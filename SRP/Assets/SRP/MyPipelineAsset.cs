using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class MyPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    Color clearColor = Color.green;
    [SerializeField]
    bool dynamicBatching;

    [SerializeField]
    bool instancing;

    [SerializeField]
    bool lightsUseLinearIntensity;


    public enum ShadowMapSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    [SerializeField]
    ShadowMapSize shadowMapSize;

    [SerializeField]
    float shadowDistance = 100f;

    public enum ShadowCascades
    {
        Zero = 0,
        Two = 2,
        Four = 4
    }
    [SerializeField]
    ShadowCascades shadowCascades = ShadowCascades.Four;
    
    [SerializeField,HideInInspector]
    float twoCascadesSplit = 0.25f;
    
    [SerializeField,HideInInspector]
    Vector3 fourCascadesSplit = new Vector3(0.67f,0.2f, 0.467f);

    Vector3 shadowCascadeSplit;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("SRP/01 - Create Basic Asset Pipeline")]
    static void CreateBasicAssetPipeline()
    {
        var instance = ScriptableObject.CreateInstance<MyPipelineAsset>();
        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/SRP/BasicAssetPipe/MyPipelineAsset.asset");
    }

    protected override RenderPipeline CreatePipeline()
    {
        Vector3 shadowCascadeSplit = shadowCascades == ShadowCascades.Four ?
            fourCascadesSplit : new Vector3(twoCascadesSplit, 0f);

        var p = new MyPipeline(
            clearColor, dynamicBatching, 
            instancing,(int)shadowMapSize,
            shadowDistance, (int)shadowCascades,
            shadowCascadeSplit);
        return p;
    }

#endif
}

public class MyPipeline : RenderPipeline
{
    private Color m_ClearColor = Color.black;
    private bool dynamicBatching = false;
    private bool instancing = false;
    private int shadowMapSize;

    int shadowCascades;
    Vector3 shadowCascadeSplit;

    public MyPipeline(Color clearColor, bool dynamicBatching,bool instancing,int shadowMapSize,float shadowDistance, int shadowCascades,Vector3 shadowCascadeSplit)
    {
        this.m_ClearColor = clearColor;
        this.dynamicBatching = dynamicBatching;
        this.instancing = instancing;
        this.shadowMapSize = shadowMapSize;
        this.shadowDistance = shadowDistance;
        this.shadowCascades = shadowCascades;
        this.shadowCascadeSplit = shadowCascadeSplit;

        GraphicsSettings.lightsUseLinearIntensity = true;
    }


    const string k_RenderCameraTag = "Render Camera";

    const int maxVisibleLights = 16;
    static int visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightDirectionsOrPositionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLightSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");
    static int unity_PerObjectLightDataID = Shader.PropertyToID("unity_PerObjectLightData");
    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    static int worldToShadowMatrixId = Shader.PropertyToID("_WorldToShadowMatrix");
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    static int shadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    static int shadowDataId = Shader.PropertyToID("_ShadowData");
    static int worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");
    static int globalShadowDataId = Shader.PropertyToID("_GlobalShadowData");
    static int worldToShadowCascadeMatricesId = Shader.PropertyToID("_WorldToShadowCascadeMatrices");
    static int cascadedShadowMapId = Shader.PropertyToID("_CascadedShadowMap");

    static int cascadedShadowMapSizeId =
    Shader.PropertyToID("_CascadedShadowMapSize");
    static int cascadedShadoStrengthId =
        Shader.PropertyToID("_CascadedShadowStrength");


    const string shadowsHardKeyword = "_SHADOWS_HARD";
    const string shadowsSoftKeyword = "_SHADOWS_SOFT";
    const string cascadedShadowsHardKeyword = "_CASCADED_SHADOWS_HARD";
    const string cascadedShadowsSoftKeyword = "_CASCADED_SHADOWS_SOFT";


    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];
    /// <summary>
    /// x is shadowStrength
    /// y is LightShadows.Soft or LightShadows.Hard
    /// z is directionLight 
    /// </summary>
    Vector4[] shadowData = new Vector4[maxVisibleLights];
    Matrix4x4[] worldToShadowMatrices = new Matrix4x4[maxVisibleLights];
    Matrix4x4[] worldToShadowCascadeMatrices = new Matrix4x4[4];

    RenderTexture shadowMap;
    RenderTexture cascadedShadowMap;

    float shadowDistance = 100;

    CommandBuffer cameraBuffer = CommandBufferPool.Get(k_RenderCameraTag);
    CommandBuffer shadowBuffer = CommandBufferPool.Get(k_RenderCameraTag);

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {

        Clear(ref visibleLightColors);
        Clear(ref visibleLightDirectionsOrPositions);
        Clear(ref visibleLightAttenuations);
        Clear(ref visibleLightSpotDirections);
        Clear(ref shadowData);
        Clear(ref worldToShadowMatrices);
        Clear(ref worldToShadowCascadeMatrices);

        foreach (var camera in cameras)
        {

            RenderOpaque(context,camera);
        }
    }

    void Clear(ref Vector4[] array)
    {
        for(int i=0; i< array.Length; i++)
        {
            array[i] = Vector3.zero;
        }
    }

    void Clear(ref Matrix4x4[] array)
    {
        for (int i = 0; i <array.Length; i++)
        {
            array[i] = Matrix4x4.zero;
        }
    }

    private void RenderOpaque(ScriptableRenderContext context , Camera camera)
    {

        ScriptableCullingParameters cullingParameters;

        if (!camera.TryGetCullingParameters(out cullingParameters))
        {
            return;
        }

#if  UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif
        ///设置方向光距离
        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);

        CullingResults cull;

        cull  = context.Cull(ref cullingParameters);

       

        if (cull.visibleLights.Length > 0)
        {
            ConfigureLights(cull);
            
            
            if (mainLightExists)
            {
                RenderCascadedShadows(context,cull);
                
            }
            else
            {
                cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            }

            if (shadowTileCount > 0)
            {
                RenderShadows(context, cull);
            }
            else
            {
                cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(shadowsSoftKeyword);
            }

            cameraBuffer.SetGlobalVector(Shader.PropertyToID("_AdditionalLightsCount"), new Vector4(cull.visibleLights.Length, 0, 0, 0));
        }
        else
        {
            cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsSoftKeyword);
            cameraBuffer.SetGlobalVector(Shader.PropertyToID("_AdditionalLightsCount"),Vector4.zero);
        }

        context.SetupCameraProperties(camera);


      

        cameraBuffer.ClearRenderTarget(true, true, Color.black);
        CameraClearFlags clearFlags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            (clearFlags & CameraClearFlags.Depth) != 0,
            (clearFlags & CameraClearFlags.Color) != 0,
            camera.backgroundColor
        );


        #region lights 



        cameraBuffer.SetGlobalVectorArray(visibleLightColorsId, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsOrPositionsId, visibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsId, visibleLightSpotDirections);


        context.ExecuteCommandBuffer(cameraBuffer);

        cameraBuffer.Clear();

        #endregion   lights 

        cameraBuffer.BeginSample("Render Opaque");

        SortingSettings sortingSettings = new SortingSettings(camera);

        DrawingSettings settings = new DrawingSettings();

        //Shader中不写LightMode时默认ShaderTagId值为“SRPDefaultUnlit”
        settings.SetShaderPassName(0, new ShaderTagId("SRPDefaultUnlit"));

        //settings.SetShaderPassName(0, new ShaderTagId("ForwardBase"));
        //settings.SetShaderPassName(1, new ShaderTagId("PrepassBase"));
        //settings.SetShaderPassName(2, new ShaderTagId("Always"));
        //settings.SetShaderPassName(3, new ShaderTagId("Vertex"));
        //settings.SetShaderPassName(4, new ShaderTagId("VertexLMRGBM"));
        //settings.SetShaderPassName(5, new ShaderTagId("VertexLM"));
        //settings.SetShaderPassName(6, new ShaderTagId("SRPDefaultUnlit"));
       



        sortingSettings.criteria = SortingCriteria.CommonOpaque;
        settings.sortingSettings = sortingSettings;


        ///动态合批设置(底层支持)
        settings.enableDynamicBatching = dynamicBatching;
        settings.enableInstancing = instancing;

        ///对camera culling layermask的进一步控制
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque, -1);
 
        context.DrawRenderers(cull, ref settings, ref filterSettings);

        cameraBuffer.EndSample("Render Opaque");

        cameraBuffer.Clear();

        cameraBuffer.BeginSample("Render Transparent");

        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        settings.sortingSettings = sortingSettings;


        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        filterSettings.layerMask = 1<<5 | 1<<0; 

        context.DrawRenderers(cull, ref settings, ref filterSettings);



        cameraBuffer.EndSample("Render Transparent");

        cameraBuffer.Clear();

        context.Submit();

        if (shadowMap)
        {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }

        if (cascadedShadowMap)
        {
            RenderTexture.ReleaseTemporary(cascadedShadowMap);
            cascadedShadowMap = null;
        }
    }

    // Cascades for Main Directional Light Only 
    bool mainLightExists;

    //配置灯光信息
    /// <summary>
    /// 多Directional会导致crash，奇怪了
    /// </summary>
    /// <param name="cull"></param>
    private void ConfigureLights(CullingResults cull)
    {
        shadowTileCount = 0;
        mainLightExists = false;

        //如果一个主光源存在，我们必须跳过渲染阴影中的第一个光源。
        for (int i = 0; i < cull.visibleLights.Length && i < maxVisibleLights; i++)
        {
            VisibleLight light = cull.visibleLights[i];
            visibleLightColors[i] = light.finalColor;
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1f;

            if (light.lightType == LightType.Directional)
            {
                Vector4 v = light.localToWorldMatrix.GetColumn(2);
                v.y *= -1;
                v.z *= -1;
                v.x *= -1;

                v.w = 0;

                visibleLightDirectionsOrPositions[i] = v;

                shadowData[i] = ConfigureShadows(i, light.light, cull);

                shadowData[i].z = 1f;

                if (i == 0 && shadowData[i].x > 0f && shadowCascades > 0)
                {
                    mainLightExists = true;
                    //cascades 阴影下不需要tilemap
                    shadowTileCount -= 1;
                }
            }
            else
            {
                visibleLightDirectionsOrPositions[i] = light.localToWorldMatrix.GetColumn(3);
                visibleLightDirectionsOrPositions[i].w = 1;
                attenuation.x = 1f /
                    Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    Vector4 v = light.localToWorldMatrix.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightSpotDirections[i] = v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos =
                        Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;


                    shadowData[i] = ConfigureShadows(i, light.light,cull);
                }

                visibleLightAttenuations[i] = attenuation;
            }
        }


        //cascades shadow设置
        if (mainLightExists || cull.visibleLights.Length > maxVisibleLights)
        {
            var lightIndices = cull.GetLightIndexMap( Unity.Collections.Allocator.Temp);
            if (mainLightExists)
            {
                lightIndices[0] = -1;
            }
            for (int i = maxVisibleLights; i < cull.visibleLights.Length; i++)
            {
                lightIndices[i] = -1;
            }
            cull.SetLightIndexMap(lightIndices);
        }
    }


    Vector4 ConfigureShadows(int lightIndex ,Light shadowLight,CullingResults cull)
    {
        Vector4 shadow = Vector4.zero;
        Bounds shadowBounds;

        if (shadowLight.shadows != LightShadows.None && cull.GetShadowCasterBounds(lightIndex, out shadowBounds))
        {
            shadowTileCount += 1;
            shadow.x = shadowLight.shadowStrength;
            shadow.y = shadowLight.shadows == LightShadows.Soft ? 1f : 0f;
            
        }

        return shadow;
    }

    RenderTexture SetShadowRenderTarget(CommandBuffer shadowBuffer)
    {
        RenderTexture texture = RenderTexture.GetTemporary(
            shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap
        );
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(
            shadowBuffer, texture,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            ClearFlag.Depth
        );
        return texture;
    }

    /// <summary>
    ///  按照光源分割纹理
    /// </summary>
    /// <param name="shadowBuffer"></param>
    /// <param name="tileIndex"></param>
    /// <param name="split"></param>
    /// <param name="tileSize"></param>
    /// <returns></returns>
    Vector2 ConfigureShadowTile(CommandBuffer shadowBuffer , int tileIndex, int split, float tileSize)
    {
        Vector2 tileOffset;
        tileOffset.x = tileIndex % split;
        tileOffset.y = tileIndex / split;

        //设置区域保持每块之间间隙 防止采样错误

        var tileViewport = new Rect(
            tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize
        );
        shadowBuffer.SetViewport(tileViewport);
        shadowBuffer.EnableScissorRect(new Rect(
            tileViewport.x + 4f, tileViewport.y + 4f,
            tileSize - 8f, tileSize - 8f
        ));
        return tileOffset;
    }

    /// <summary>
    /// 时间空间转阴影空间矩阵
    /// </summary>
    /// <param name="viewMatrix"></param>
    /// <param name="projectionMatrix"></param>
    /// <param name="worldToShadowMatrix"></param>
    void CalculateWorldToShadowMatrix(
        ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix,
        out Matrix4x4 worldToShadowMatrix
    )
    {
        //This property is true if the current platform uses a reversed depth buffer (where values range from 1 at the near plane and 0 at far plane), and false if the depth buffer is normal (0 is near, 1 is far).
        //dx 1 at the near plane and 0 at far plane
        //gl 0 at the near plane and 1 at far plane
        if (SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }
        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
        worldToShadowMatrix =
            scaleOffset * (projectionMatrix * viewMatrix);
    }



    void RenderCascadedShadows(ScriptableRenderContext context,CullingResults cull)
    {

        float tileSize = shadowMapSize / 2;
        cascadedShadowMap = SetShadowRenderTarget(shadowBuffer);
        shadowBuffer.BeginSample("Render CascadesShadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
        Light shadowLight = cull.visibleLights[0].light;
        shadowBuffer.SetGlobalFloat(
            shadowBiasId, shadowLight.shadowBias
        );
        var shadowSettings = new ShadowDrawingSettings(cull, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        for (int i = 0; i < shadowCascades; i++)
        {
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                0, i, shadowCascades, shadowCascadeSplit, (int)tileSize,
                shadowLight.shadowNearPlane,
                out viewMatrix, out projectionMatrix, out splitData
            );

            Vector2 tileOffset = ConfigureShadowTile(shadowBuffer,i, 2, tileSize);
            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            shadowSettings.splitData = splitData;

            context.DrawShadows(ref shadowSettings);
            CalculateWorldToShadowMatrix(
                ref viewMatrix, ref projectionMatrix,
                out worldToShadowCascadeMatrices[i]
            );
            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            worldToShadowCascadeMatrices[i] =
                tileMatrix * worldToShadowCascadeMatrices[i];
        }

        shadowBuffer.DisableScissorRect();
        shadowBuffer.SetGlobalTexture(cascadedShadowMapId, cascadedShadowMap);
        shadowBuffer.SetGlobalMatrixArray(
            worldToShadowCascadeMatricesId, worldToShadowCascadeMatrices
        );

        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(
            cascadedShadowMapSizeId, new Vector4(
                invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize
            )
        );
        shadowBuffer.SetGlobalFloat(
            cascadedShadoStrengthId, shadowLight.shadowStrength
        );
        bool hard = shadowLight.shadows == LightShadows.Hard;
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsHardKeyword, hard);
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsSoftKeyword, !hard);


        shadowBuffer.EndSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

    //标记产生阴影的光源数量
    int  shadowTileCount;
    private  void RenderShadows(ScriptableRenderContext context,CullingResults cull)
    {
        #region 根据光源计算分割区域

        int split;

        if (shadowTileCount <= 1)
        {
            split = 1;
        }
        else if (shadowTileCount <= 4)
        {
            split = 2;
        }
        else if (shadowTileCount <= 9)
        {
            split = 3;
        }
        else
        {
            split = 4;
        }

        float tileSize = shadowMapSize / split;
        float tileScale = 1f / split;

        Rect tileViewport = new Rect(0f, 0f, tileSize, tileSize);

        int tileIndex = 0;

        #endregion

        //开启开关
        bool hardShadows = false;
        bool softShadows = false;


        # region Rendered shadow map.

        shadowMap = SetShadowRenderTarget(shadowBuffer);


        //指定渲染目标后不需要清除 - RenderBufferLoadAction.DontCare
        //GPU渲染完毕后保存在内存中 - RenderBufferStoreAction.Store
        CoreUtils.SetRenderTarget(shadowBuffer, shadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Depth);

        shadowBuffer.BeginSample("Render Shadow");

        shadowBuffer.SetGlobalVector(
            globalShadowDataId, new Vector4(tileScale, shadowDistance * shadowDistance)
        );

        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();



        for (int i = 0; i < cull.visibleLights.Length; i++)
        {
            if (i == maxVisibleLights)
            {
                break;
            }

            if (shadowData[i].x <= 0f)
            {
                continue;
            }

            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;

            ///支持multiplelight，标记是有阴影产生
            bool validShadows;

            //检查方向光标记,处理不同灯光类型
            if (shadowData[i].z > 0f)
            {
                validShadows = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.right,
                    (int)tileSize, cull.visibleLights[i].light.shadowNearPlane, 
                    out viewMatrix, out projectionMatrix, out splitData);
            }
            else
            {
                validShadows = cull.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
            }

            if(!validShadows)
            {
                shadowData[i].x = 0;
                continue;
            }

            
            var tileOffset = ConfigureShadowTile(shadowBuffer, i, split, tileSize);

            shadowData[i].z = tileOffset.x * tileScale;
            shadowData[i].w = tileOffset.y * tileScale;

            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            shadowBuffer.SetGlobalFloat(
                shadowBiasId, cull.visibleLights[i].light.shadowBias
            );

            //解决深度纹理 Shadow Acne
            //shadowBuffer.SetGlobalFloat(shadowBiasId, cull.visibleLights[i].light.shadowBias);
            //shadowBuffer.SetGlobalFloat(shadowStrengthId, cull.visibleLights[i].light.shadowStrength);



            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            var shadowSettings = new ShadowDrawingSettings(cull, i);

            shadowSettings.splitData = splitData;
            //默认Pass { "LightMode" = "ShadowCaster" }
            context.DrawShadows(ref shadowSettings);

        

        #endregion

        #region 构建矩阵

            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowMatrices[i]);

            //if (split > 1)
            //{
            //
            //    var tileMatrix = Matrix4x4.identity;
            //    tileMatrix.m00 = tileMatrix.m11 = tileScale;
            //    tileMatrix.m03 = tileOffsetX * tileScale;
            //    tileMatrix.m13 = tileOffsetY * tileScale;
            //
            //    //更改采样矩阵，之前是整个屏幕，现在缩放到每个块上
            //    worldToShadowMatrices[i] = tileMatrix * worldToShadowMatrices[i];
            //}

            tileIndex += 1;


            if (shadowData[i].y <= 0f)
            {
                hardShadows = true;
            }
            else
            {
                softShadows = true;
            }
        }

        #endregion

        //传递纹理



        //if (split > 1)  
            shadowBuffer.DisableScissorRect();
        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);
        shadowBuffer.SetGlobalMatrixArray(worldToShadowMatricesId, worldToShadowMatrices);
        shadowBuffer.SetGlobalVectorArray(shadowDataId, shadowData);

        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(
            shadowMapSizeId, new Vector4(
                invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize
            )
        );


        //if (cull.visibleLights[0].light.shadows == LightShadows.Soft)
        //{
        //    shadowBuffer.EnableShaderKeyword(shadowsSoftKeyword);
        //}
        //else
        //{
        //    shadowBuffer.DisableShaderKeyword(shadowsSoftKeyword);
        //}

        //CoreUtils.SetKeyword(
        //shadowBuffer, shadowsSoftKeyword,
        //cull.visibleLights[0].light.shadows == LightShadows.Soft );


        CoreUtils.SetKeyword(shadowBuffer, shadowsHardKeyword, hardShadows);
        CoreUtils.SetKeyword(shadowBuffer, shadowsSoftKeyword, softShadows);

        shadowBuffer.EndSample("Render Shadow");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

    }
}