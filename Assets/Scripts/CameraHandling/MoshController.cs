using System;
using System.Collections;
using System.Timers;
using UnityEngine;
using UnityEngine.Serialization;

namespace CameraHandling
{
    public class MoshController : MonoBehaviour
    {
        #region Mosh Manipulation Variables

        [SerializeField] [Tooltip("Size of Macroblock.")]
        private int m_BlockSize = 16;
        
        public int BlockSize
        {
            get => Mathf.Clamp(m_BlockSize,0, 4);
            set => m_BlockSize = value;
        }
        
        [SerializeField, Range(0, 2)] [Tooltip("Scale factor for velocity vectors.")]
        private float m_VelocityVectorScale = 0.8f;

        public float VelocityVectorScale
        {
            get => m_VelocityVectorScale;
            set => m_VelocityVectorScale = value;
        }

        [SerializeField, Range(0, 1)] [Tooltip("The larger value makes the stronger noise.")]
        private float m_Entropy = 0.5f;

        public float Entropy
        {
            get => m_Entropy;
            set => m_Entropy = value;
        }
        
        [SerializeField, Range(0.5f, 4.0f)]
        private float m_NoiseContrast = 1;
        
        /// Contrast of stripe-shaped noise.
        public float NoiseContrast
        {
            get => m_NoiseContrast;
            set => m_NoiseContrast = value;
        }

        [SerializeField, Range(0, 2)] 
        float m_Diffusion = 0.4f;
        
        /// Amount of random displacement.
        public float Diffusion
        {
            get => m_Diffusion;
            set => m_Diffusion = value;
        }

        #endregion
        
        [SerializeField] 
        private Shader m_Shader;
        
        //Note: we wont be able to get motion vectors of video so no real moshing for videoitslef
        [SerializeField] 
        private RenderTexture m_VideoRenderTexture;

        private Material m_Material;
        
        //Blit changes RenderTexture.active. Store the active render texture before you use Blit if you need to use it afterwards.
        private RenderTexture m_WorkBuffer; 
        private RenderTexture m_DispBuffer; 

        private int m_MoshSequence;
        private int m_LastFrame;
        
        [SerializeField][Tooltip("Time interval for moshing on Cam View, o saniyede bir")]
        private float m_CamMoshInterval;
        [SerializeField][Tooltip("Duration to stay on Camera view")]
        private float m_StayOnCamTime;
        
        [SerializeField][Tooltip("Time interval for moshing on VIDEO View, o saniyede bir")]
        private float m_VideoInterval = 0;
        [SerializeField][Tooltip("Duration to stay on VIDEO view")]
        private float m_StayOnVideoTime;

        private int m_Switcher = 0;
        
        private float m_Timer;
        
        private float m_Interval = 3.0f;
        private float Interval => Mathf.Max(1.0f / 30, m_Interval);
        
        // Internally Unity keeps a pool of temporary render textures, so a call to
        // GetTemporary most often just returns an already created one (if the size and format matches).
        // These temporary render textures are actually destroyed when they aren't used for a couple of frames.
        //If you are doing a series of post-processing "blits", it's best for performance to get and release a
        //temporary render texture for each blit, instead of getting one or two render textures upfront and reusing them.
        //Allocate a temporary render texture
        private RenderTexture NewWorkBuffer(RenderTexture source)
        {
            return RenderTexture.GetTemporary(source.width, source.height);
        }

        private RenderTexture NewDispBuffer(RenderTexture source)
        {
            var rt = RenderTexture.GetTemporary(
                source.width / m_BlockSize,
                source.height / m_BlockSize,
                //In linear color space, set GL.sRGBWrite before using Blit, to make sure the sRGB-to-linear color conversion is what you expect.
                0, RenderTextureFormat.ARGBHalf
            );
            rt.filterMode = FilterMode.Point;
            return rt;
        }

        private void ReleaseBuffer(RenderTexture buffer)
        {
            if (buffer != null) RenderTexture.ReleaseTemporary(buffer);
        }
        
        void OnEnable()
        {
            m_Material = new Material(m_Shader);
            m_Material.hideFlags = HideFlags.DontSave;

            // TODO
            GetComponent<Camera>().depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;

            m_MoshSequence = 0;
        }

        void OnDisable()
        {
            ReleaseBuffer(m_WorkBuffer);
            m_WorkBuffer = null;

            ReleaseBuffer(m_DispBuffer);
            m_DispBuffer = null;

            DestroyImmediate(m_Material);
            m_Material = null;
        }
        
        IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSeconds(Interval);
                KickGlitch();
            }
        }

        private void Update()
        {
            m_Timer += Time.deltaTime;
            
            var currentDur = m_Switcher % 2 == 0 ? m_StayOnCamTime : m_StayOnVideoTime;

            if (!(m_Timer > currentDur)) 
                return;
            
            m_Timer = 0;

            var nextDur = (m_Switcher + 1) % 2 == 0 ? m_StayOnCamTime : m_StayOnVideoTime;
            if (nextDur <= 0)
            {
                Debug.Log("Next duration is 0, cannot switch.");
                return;
            }
            
            m_Switcher++;
        }

        //Gets called after cam finished rendering, allowing you to manipulate the texture
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            m_Material.SetFloat("_BlockSize", m_BlockSize);
            m_Material.SetFloat("_Quality", 1 - m_Entropy);
            m_Material.SetFloat("_Contrast", m_NoiseContrast);
            m_Material.SetFloat("_Velocity", m_VelocityVectorScale);
            m_Material.SetFloat("_Diffusion", m_Diffusion);
            
            //Switch between cam and video
            var viewTexture = m_Switcher % 2 == 0 ? source : m_VideoRenderTexture;
            
            
            // Step 0: no effect, just keep the last frame.
            if (m_MoshSequence == 0)
            {
                // Initialize and update the working buffer with the current frame.
                ReleaseBuffer(m_WorkBuffer);
                m_WorkBuffer = NewWorkBuffer(viewTexture);
                //If you don't provide mat while blitting, Unity uses a default material.
                Graphics.Blit(viewTexture, m_WorkBuffer);

                // Blit without effect. Not sure why we are doing this......... and actually we should not blit same source and dest... TODO
                Graphics.Blit(viewTexture, destination);
            }
            // Step 1: start effect, no moshing.
            else if (m_MoshSequence == 1)
            {
                // Initialize the displacement buffer.
                ReleaseBuffer(m_DispBuffer);
                m_DispBuffer = NewDispBuffer(viewTexture);
           
                //If the value is -1, Unity draws all the passes in mat. Otherwise, Unity draws only the pass you set pass to, The default value is -1.
                //Initializes the displacement buffer with the first pass of the mosh material. Which does nothing
                Graphics.Blit(null, m_DispBuffer, m_Material, 0);

                // Simply blit the working buffer because motion vectors might not be ready (because of sudden camera pos change) TODO
                Graphics.Blit(m_WorkBuffer, destination);

                m_MoshSequence++;
            }
            else
            {
                // Final step: apply effect cont.

                if (Time.frameCount != m_LastFrame)
                {
                    // Update the displacement buffer with the adding the second pass of the mosh material.
                    var newDisp = NewDispBuffer(viewTexture);
                    Graphics.Blit(m_DispBuffer, newDisp, m_Material, 1);
                    ReleaseBuffer(m_DispBuffer);
                    m_DispBuffer = newDisp;

                    // Moshing!
                    m_Material.SetTexture("_WorkTex", m_WorkBuffer);
                    m_Material.SetTexture("_DispTex", m_DispBuffer);
                    
                    //Get the current view texture, and blit it with itself by adding the mosh mat with the third pass (which has previous frame 
                    //texture, and displaced texture.
                    var newWork = NewWorkBuffer(viewTexture);
                    Graphics.Blit(viewTexture, newWork, m_Material, 2);
                    
                    // Update the working buffer with the result.
                    ReleaseBuffer(m_WorkBuffer);
                    m_WorkBuffer = newWork;

                    m_LastFrame = Time.frameCount;
                }

                // Blit the result to the cam fully
                Graphics.Blit(m_WorkBuffer, destination);
            }
            
        }
        
        void KickGlitch()
        {
            Glitch();
            
            m_Interval = m_Switcher % 2 == 0 ? m_CamMoshInterval : m_VideoInterval;
        }

        /// Start glitching.
        private void Glitch()
        {
            m_MoshSequence = 1;
        }

        /// Stop glitching.
        public void Reset()
        {
            m_MoshSequence = 0;
        }
    }
}