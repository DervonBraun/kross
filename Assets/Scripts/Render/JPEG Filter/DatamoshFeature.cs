using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Render
{
    public class DatamoshRenderGraphFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material pass1Material; // Jpegish
            public Material pass2Material; // Temporal
        
            [Range(0.1f, 5.0f)]
            public float motionSensitivity = 1.5f;
        
            [Tooltip("Порог дистанции для сброса истории (смена сцены/телепорт)")]
            public float teleportThreshold = 10.0f; 
        
            public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public Settings settings = new Settings();
        DatamoshRenderPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new DatamoshRenderPass(settings);
            m_ScriptablePass.renderPassEvent = settings.renderEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.pass1Material == null || settings.pass2Material == null) return;
            renderer.EnqueuePass(m_ScriptablePass);
        }

        protected override void Dispose(bool disposing)
        {
            m_ScriptablePass.Dispose();
        }

        // --- RENDER PASS ---
        class DatamoshRenderPass : ScriptableRenderPass
        {
            Settings settings;

            // Данные для каждого прохода внутри графа
            class PassData
            {
                public TextureHandle source;
                public TextureHandle destination;
                public Material material;
            }

            // Данные камеры (история, позиция)
            class CameraData
            {
                public RTHandle historyHandle;
                public Vector3 lastPos;
                public Quaternion lastRot;
                public bool isFirstFrame = true;
            }
        
            Dictionary<Camera, CameraData> m_CameraHistory = new Dictionary<Camera, CameraData>();

            // Shader Property IDs
            static readonly int _CompMotion = Shader.PropertyToID("_CompMotion");
            static readonly int _CompKeyframe = Shader.PropertyToID("_CompKeyframe");
            static readonly int _CompHistoryValid = Shader.PropertyToID("_CompHistoryValid");
            static readonly int _CompHistoryTex = Shader.PropertyToID("_CompHistoryTex");

            public DatamoshRenderPass(Settings settings)
            {
                this.settings = settings;
            }

            public void Dispose()
            {
                foreach (var kvp in m_CameraHistory)
                {
                    kvp.Value.historyHandle?.Release();
                }
                m_CameraHistory.Clear();
            }

            // В URP 17 это основной метод
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // 1. Получаем/Инициализируем Persistent данные (Историю)
                Camera cam = cameraData.camera;
                if (!m_CameraHistory.TryGetValue(cam, out CameraData camData))
                {
                    camData = new CameraData();
                    m_CameraHistory[cam] = camData;
                }

                // Проверка размера истории (ресайз, если экран изменился)
                var cameraDesc = cameraData.cameraTargetDescriptor;
                cameraDesc.depthBufferBits = 0; // Нам не нужен Z-буфер в истории
                cameraDesc.msaaSamples = 1;

                bool historyResized = false;
                // ReAllocateIfNeeded вернет true, если пересоздал текстуру
                if (RenderingUtils.ReAllocateHandleIfNeeded(ref camData.historyHandle, cameraDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"History_Cam_{cam.GetInstanceID()}"))
                {
                    camData.isFirstFrame = true;
                    historyResized = true;
                }
// 2. Расчет Motion (Логика движения)
                float motion = 0f;
                float keyframe = 0f;

                if (!camData.isFirstFrame)
                {
                    float dist = Vector3.Distance(cam.transform.position, camData.lastPos);
                    float angle = Quaternion.Angle(cam.transform.rotation, camData.lastRot);

                    if (dist > settings.teleportThreshold)
                    {
                        keyframe = 1.0f; // Телепорт
                    }
                    else
                    {
                        float rawMotion = (dist * 0.5f) + (angle * 0.1f); 
                        motion = Mathf.Clamp01(rawMotion * settings.motionSensitivity);
                    }
                }
                else
                {
                    keyframe = 1.0f; // Первый кадр
                }

                camData.lastPos = cam.transform.position;
                camData.lastRot = cam.transform.rotation;
                camData.isFirstFrame = false;

                // 3. Обновление материалов
                // (Делаем это ДО записи в граф, чтобы значения попали в DrawCall)
                settings.pass1Material.SetFloat(_CompMotion, motion);
            
                settings.pass2Material.SetFloat(_CompMotion, motion);
                settings.pass2Material.SetFloat(_CompKeyframe, keyframe);
                settings.pass2Material.SetFloat(_CompHistoryValid, historyResized ? 0.0f : 1.0f);
            
                // ВАЖНО: Мы не можем просто сделать SetTexture(_CompHistoryTex, handle) здесь.
                // TextureHandle существует только внутри графа. Мы привяжем его внутри Pass 2.

                // 4. Создаем Render Graph ресурсы
                TextureHandle sourceHandle = resourceData.activeColorTexture;
            
                // Создаем временную текстуру для Pass 1 (JPEG)
                TextureHandle tempTextureHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraDesc, "Datamosh_Temp", false);

                // Импортируем "внешнюю" RTHandle истории в граф, чтобы использовать ее как текстуру
                TextureHandle historyTextureHandle = renderGraph.ImportTexture(camData.historyHandle);


                // --- PASS 1: JPEG Compression (Source -> Temp) ---
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Datamosh Pass 1 (JPEG)", out var passData))
                {
                    passData.source = sourceHandle;
                    passData.destination = tempTextureHandle;
                    passData.material = settings.pass1Material;

                    builder.UseTexture(passData.source);      // Читаем камеру
                    builder.SetRenderAttachment(passData.destination, 0); // Пишем во временную

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        // Blitter автоматически использует _BlitTexture как source
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                // --- PASS 2: Temporal (Temp + History -> Source) ---
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Datamosh Pass 2 (Temporal)", out var passData))
                {
                    passData.source = tempTextureHandle;     // Вход - результат Pass 1
                    passData.destination = sourceHandle;     // Выход - обратно на экран
                    passData.material = settings.pass2Material;

                    builder.UseTexture(passData.source);
                    builder.UseTexture(historyTextureHandle); // Указываем зависимость от истории
                    builder.SetRenderAttachment(passData.destination, 0);

                    // Захватываем ID шейдера, чтобы использовать внутри лямбды
                    int historyID = _CompHistoryTex;

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        // Простое копирование
                        Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                    });
                }
            }
        }
    }
}