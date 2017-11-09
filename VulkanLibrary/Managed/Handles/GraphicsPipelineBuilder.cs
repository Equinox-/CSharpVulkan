using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VulkanLibrary.Unmanaged;
using VulkanLibrary.Unmanaged.Handles;

namespace VulkanLibrary.Managed.Handles
{
    public class GraphicsPipelineBuilder<TAttachment>
    {
        private readonly RenderPass _pass;
        private readonly uint _subpassId;
        private readonly Func<TAttachment, uint> _attachmentLookup;
        private readonly PipelineLayout _pipelineLayout;
        private readonly List<GCHandle> _pins = new List<GCHandle>();
        private readonly List<IntPtr> _strPins = new List<IntPtr>();

        private readonly List<VkPipelineShaderStageCreateInfo> _stages = new List<VkPipelineShaderStageCreateInfo>();

        private readonly Dictionary<uint, VkVertexInputBindingDescription> _vertexBindingDescriptions =
            new Dictionary<uint, VkVertexInputBindingDescription>();

        private readonly Dictionary<uint, VkVertexInputAttributeDescription> _vertexAttributeDescriptions =
            new Dictionary<uint, VkVertexInputAttributeDescription>();

        private readonly Dictionary<TAttachment, VkPipelineColorBlendAttachmentState> _blendAttachmentStates =
            new Dictionary<TAttachment, VkPipelineColorBlendAttachmentState>();

        private readonly List<VkViewport> _viewports = new List<VkViewport>();
        private readonly List<VkRect2D> _viewportScissors = new List<VkRect2D>();

        private VkPipelineInputAssemblyStateCreateInfo _asmInfo;
        private VkPipelineTessellationStateCreateInfo _tessInfo;
        private VkPipelineRasterizationStateCreateInfo _rasterInfo;
        private VkPipelineMultisampleStateCreateInfo _multisampleInfo;
        private VkPipelineDepthStencilStateCreateInfo _depthStencilInfo;
        private VkPipelineColorBlendStateCreateInfo _colorBlendInfo;
        private readonly List<VkDynamicState> _dynamicStates = new List<VkDynamicState>();

        public GraphicsPipelineBuilder(RenderPass pass, uint subpass, PipelineLayout pipelineLayout,
            Func<TAttachment, uint> attachmentLookup)
        {
            _attachmentLookup = attachmentLookup;
            _pipelineLayout = pipelineLayout;
            _pass = pass;
            _subpassId = subpass;

            unsafe
            {
                _asmInfo.SType = VkStructureType.PipelineInputAssemblyStateCreateInfo;
                _asmInfo.Flags = 0;
                _asmInfo.PNext = (void*) 0;

                _tessInfo.SType = VkStructureType.PipelineTessellationStateCreateInfo;
                _tessInfo.Flags = 0;
                _tessInfo.PNext = (void*) 0;
                _tessInfo.PatchControlPoints = 0;

                _rasterInfo.SType = VkStructureType.PipelineRasterizationStateCreateInfo;
                _rasterInfo.Flags = 0;
                _rasterInfo.PNext = (void*) 0;
                _rasterInfo.DepthBiasEnable = false;
                _rasterInfo.DepthClampEnable = false;
                _rasterInfo.RasterizerDiscardEnable = false;
                _rasterInfo.FrontFace = VkFrontFace.CounterClockwise;
                _rasterInfo.LineWidth = 1.0f;

                _multisampleInfo.SType = VkStructureType.PipelineMultisampleStateCreateInfo;
                _multisampleInfo.Flags = 0;
                _multisampleInfo.PNext = (void*) 0;
                _multisampleInfo.RasterizationSamples = VkSampleCountFlag.Count1;
                _multisampleInfo.SampleShadingEnable = false;

                _depthStencilInfo.SType = VkStructureType.PipelineDepthStencilStateCreateInfo;
                _depthStencilInfo.Flags = 0;
                _depthStencilInfo.PNext = (void*) 0;
                _depthStencilInfo.DepthTestEnable = false;
                _depthStencilInfo.StencilTestEnable = false;
                _depthStencilInfo.DepthBoundsTestEnable = false;

                _colorBlendInfo.SType = VkStructureType.PipelineColorBlendStateCreateInfo;
                _colorBlendInfo.Flags = 0;
                _colorBlendInfo.PNext = (void*) 0;
                _colorBlendInfo.LogicOpEnable = false;
            }
        }

        public class ShaderStageBuilder
        {
            private readonly GraphicsPipelineBuilder<TAttachment> _builder;
            private VkPipelineShaderStageCreateInfo _shaderInfo;
            private readonly string _entryPoint;
            private VkSpecializationInfo? _specializationInfo;

            internal ShaderStageBuilder(GraphicsPipelineBuilder<TAttachment> builder, VkShaderModule handle,
                VkShaderStageFlag stage, string entryPoint)
            {
                _builder = builder;
                _specializationInfo = null;
                unsafe
                {
                    _shaderInfo = new VkPipelineShaderStageCreateInfo()
                    {
                        SType = VkStructureType.PipelineShaderStageCreateInfo,
                        PNext = (void*) 0,
                        Module = handle,
                        Flags = 0,
                        Stage = stage
                    };
                }
                _entryPoint = entryPoint;
            }

            public GraphicsPipelineBuilder<TAttachment> Commit()
            {
                if (_specializationInfo.HasValue)
                    throw new NotSupportedException();
                var strName = Marshal.StringToHGlobalAnsi(_entryPoint);
                _builder._strPins.Add(strName);
                unsafe
                {
                    _shaderInfo.PName = (byte*) strName.ToPointer();
                    _shaderInfo.PSpecializationInfo = (VkSpecializationInfo*) 0;
                }
                _builder._stages.Add(_shaderInfo);
                return _builder;
            }
        }

        public ShaderStageBuilder ShaderStage(ShaderModule shader, VkShaderStageFlag flags, string entryPoint)
        {
            return new ShaderStageBuilder(this, shader.Handle, flags, entryPoint);
        }

        public GraphicsPipelineBuilder<TAttachment> VertexBinding(uint binding, uint stride,
            VkVertexInputRate rate = VkVertexInputRate.Vertex)
        {
            _vertexBindingDescriptions.Add(binding, new VkVertexInputBindingDescription()
            {
                Binding = binding,
                Stride = stride,
                InputRate = rate
            });
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> VertexAttribute(uint location, uint binding, VkFormat format,
            uint offset)
        {
            _vertexAttributeDescriptions.Add(location, new VkVertexInputAttributeDescription()
            {
                Location = location,
                Binding = binding,
                Format = format,
                Offset = offset
            });
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> Assembly(VkPrimitiveTopology topology,
            bool primitiveRestartEnable = false)
        {
            _asmInfo.Topology = topology;
            _asmInfo.PrimitiveRestartEnable = primitiveRestartEnable;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> Tesselation(uint count)
        {
            _tessInfo.PatchControlPoints = count;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> Viewport(VkViewport viewport, VkRect2D scissors)
        {
            _viewports.Add(viewport);
            _viewportScissors.Add(scissors);
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DepthBiasOn(float clamp, float constantFactor, float slopeFactor)
        {
            _rasterInfo.DepthBiasEnable = true;
            _rasterInfo.DepthBiasClamp = clamp;
            _rasterInfo.DepthBiasConstantFactor = constantFactor;
            _rasterInfo.DepthBiasSlopeFactor = slopeFactor;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DepthBiasOff()
        {
            _rasterInfo.DepthBiasEnable = false;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DepthClamp(bool enable)
        {
            _rasterInfo.DepthClampEnable = enable;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> RasterizerDiscard(bool enable)
        {
            _rasterInfo.RasterizerDiscardEnable = enable;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> Raster(VkPolygonMode triangleMode, VkCullModeFlag cullMode,
            VkFrontFace frontFace = VkFrontFace.CounterClockwise)
        {
            _rasterInfo.PolygonMode = triangleMode;
            _rasterInfo.CullMode = cullMode;
            _rasterInfo.FrontFace = frontFace;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> SampleShadingOn(float minSampleShading)
        {
            _multisampleInfo.SampleShadingEnable = true;
            _multisampleInfo.MinSampleShading = minSampleShading;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> SampleShadingOff()
        {
            _multisampleInfo.SampleShadingEnable = false;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> AlphaToCoverage(bool enable)
        {
            _multisampleInfo.AlphaToCoverageEnable = enable;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> AlphaToOne(bool enable)
        {
            _multisampleInfo.AlphaToOneEnable = enable;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> SampleMask()
        {
            throw new NotImplementedException();
        }

        public GraphicsPipelineBuilder<TAttachment> DepthTest(bool enable, VkCompareOp op = VkCompareOp.LessOrEqual)
        {
            _depthStencilInfo.DepthTestEnable = enable;
            _depthStencilInfo.DepthCompareOp = op;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DepthWrite(bool enable)
        {
            _depthStencilInfo.DepthWriteEnable = enable;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> StencilTestOn(VkStencilOpState front, VkStencilOpState back)
        {
            _depthStencilInfo.StencilTestEnable = true;
            _depthStencilInfo.Front = front;
            _depthStencilInfo.Back = back;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> StencilTestOff()
        {
            _depthStencilInfo.StencilTestEnable = false;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DepthBoundsOn(float min, float max)
        {
            _depthStencilInfo.DepthBoundsTestEnable = true;
            _depthStencilInfo.MinDepthBounds = min;
            _depthStencilInfo.MaxDepthBounds = max;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DepthBoundsOff()
        {
            _depthStencilInfo.DepthBoundsTestEnable = false;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> LogicOp(VkLogicOp op = VkLogicOp.NoOp)
        {
            _colorBlendInfo.LogicOp = op;
            _colorBlendInfo.LogicOpEnable = op != VkLogicOp.NoOp;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> BlendConstants(VkColor color)
        {
            unsafe
            {
                fixed (float* blendConstants = _colorBlendInfo.BlendConstants)
                {
                    blendConstants[0] = color.R;
                    blendConstants[1] = color.G;
                    blendConstants[2] = color.B;
                    blendConstants[3] = color.A;
                }
            }
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> AttachmentBlendOff(TAttachment attachment, VkColorComponentFlag writeMask =
            VkColorComponentFlag.R | VkColorComponentFlag.G | VkColorComponentFlag.B | VkColorComponentFlag.A)
        {
            _blendAttachmentStates.Add(attachment, new VkPipelineColorBlendAttachmentState()
            {
                BlendEnable = false,
                ColorWriteMask = writeMask
            });
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> AttachmentBlendOn(TAttachment attachment,
            VkBlendFactor srcColorFactor, VkBlendFactor dstColorFactor, VkBlendOp colorOp,
            VkBlendFactor? srcAlphaFactor = null, VkBlendFactor? dstAlphaFactor = null,
            VkBlendOp? alphaOp = null, VkColorComponentFlag writeMask =
                VkColorComponentFlag.R | VkColorComponentFlag.G | VkColorComponentFlag.B | VkColorComponentFlag.A)
        {
            if (!srcAlphaFactor.HasValue)
                srcAlphaFactor = srcColorFactor;
            if (!dstAlphaFactor.HasValue)
                dstAlphaFactor = dstColorFactor;
            if (!alphaOp.HasValue)
                alphaOp = colorOp;
            _blendAttachmentStates.Add(attachment, new VkPipelineColorBlendAttachmentState()
            {
                BlendEnable = true,
                SrcColorBlendFactor = srcColorFactor,
                DstColorBlendFactor = dstColorFactor,
                ColorBlendOp = colorOp,
                SrcAlphaBlendFactor = srcAlphaFactor.Value,
                DstAlphaBlendFactor = dstAlphaFactor.Value,
                AlphaBlendOp = alphaOp.Value,
                ColorWriteMask = writeMask
            });
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DynamicStateOff()
        {
            _dynamicStates.Clear();
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> DynamicState(VkDynamicState state)
        {
            _dynamicStates.Add(state);
            return this;
        }

        private Pipeline _basePipeline = null;
        private int _basePipelineIndex;

        public GraphicsPipelineBuilder<TAttachment> Derivative(Pipeline @base, int baseIndex)
        {
            _basePipeline = @base;
            _basePipelineIndex = baseIndex;
            return this;
        }

        public GraphicsPipelineBuilder<TAttachment> NoDerivative()
        {
            _basePipeline = null;
            return this;
        }

        private VkPipelineCreateFlag _flags;

        public GraphicsPipelineBuilder<TAttachment> Flags(VkPipelineCreateFlag flags)
        {
            _flags = flags;
            return this;
        }

        public Pipeline Build()
        {
            var stages = _stages.ToArray();
            var vertexBindings = new VkVertexInputBindingDescription[_vertexBindingDescriptions.Count];
            foreach (var kv in _vertexBindingDescriptions)
                vertexBindings[kv.Key] = kv.Value;
            var attributeBindings = new VkVertexInputAttributeDescription[_vertexAttributeDescriptions.Count];
            foreach (var kv in _vertexAttributeDescriptions)
                attributeBindings[kv.Key] = kv.Value;
            var blendStates = new VkPipelineColorBlendAttachmentState[_blendAttachmentStates.Count];
            foreach (var kv in _blendAttachmentStates)
                blendStates[_attachmentLookup(kv.Key)] = kv.Value;
            var viewports = _viewports.ToArray();
            var scissors = _viewportScissors.ToArray();
            var dynamicStates = _dynamicStates.ToArray();
            

            try
            {
                unsafe
                {
                    fixed (VkViewport* viewportPtr = viewports)
                    fixed (VkRect2D* scissorPtr = scissors)
                    fixed (VkDynamicState* dynamicPtr = dynamicStates)
                    fixed (VkPipelineColorBlendAttachmentState* blendPtr = blendStates)
                    fixed (VkVertexInputBindingDescription* vertexPtr = vertexBindings)
                    fixed (VkVertexInputAttributeDescription* attributePtr = attributeBindings)
                    fixed (VkPipelineShaderStageCreateInfo* stagePtr = stages)
                    {
                        if (_basePipeline != null)
                            _flags |= VkPipelineCreateFlag.Derivative;
                        else
                            _flags &= ~VkPipelineCreateFlag.Derivative;

                        var vertexInputState = new VkPipelineVertexInputStateCreateInfo()
                        {
                            SType = VkStructureType.PipelineVertexInputStateCreateInfo,
                            PNext = (void*) 0,
                            Flags = 0,
                            VertexBindingDescriptionCount = (uint) vertexBindings.Length,
                            PVertexBindingDescriptions = vertexPtr,
                            VertexAttributeDescriptionCount = (uint) attributeBindings.Length,
                            PVertexAttributeDescriptions = attributePtr
                        };

                        var assemblyState = _asmInfo;
                        var tessState = _tessInfo;

                        var viewportState = new VkPipelineViewportStateCreateInfo()
                        {
                            SType = VkStructureType.PipelineViewportStateCreateInfo,
                            PNext = (void*) 0,
                            Flags = 0,
                            ViewportCount = (uint) viewports.Length,
                            PViewports = viewportPtr,
                            ScissorCount = (uint) scissors.Length,
                            PScissors = scissorPtr
                        };

                        var rasterState = _rasterInfo;
                        var multisampleState = _multisampleInfo;
                        var depthStencilState = _depthStencilInfo;
                        var colorBlendState = _colorBlendInfo;
                        colorBlendState.AttachmentCount = (uint) blendStates.Length;
                        colorBlendState.PAttachments = blendPtr;

                        var dynamicState = new VkPipelineDynamicStateCreateInfo()
                        {
                            SType = VkStructureType.PipelineDynamicStateCreateInfo,
                            PNext = (void*) 0,
                            Flags = 0,
                            DynamicStateCount = (uint) dynamicStates.Length,
                            PDynamicStates = dynamicPtr
                        };
                        
                        var info = new VkGraphicsPipelineCreateInfo()
                        {
                            SType = VkStructureType.GraphicsPipelineCreateInfo,
                            PNext = (void*) 0,
                            Flags = _flags,
                            StageCount = (uint) stages.Length,
                            PStages = stagePtr,
                            PVertexInputState = &vertexInputState,
                            PInputAssemblyState = &assemblyState,
                            PTessellationState = &tessState,
                            PViewportState = &viewportState,
                            PRasterizationState = &rasterState,
                            PMultisampleState = &multisampleState,
                            PDepthStencilState = &depthStencilState,
                            PColorBlendState = &colorBlendState,
                            PDynamicState =
                                dynamicStates.Length > 0 ? &dynamicState : (VkPipelineDynamicStateCreateInfo*) 0,
                            Layout = _pipelineLayout.Handle,
                            RenderPass = _pass.Handle,
                            Subpass = _subpassId,
                            BasePipelineHandle = _basePipeline?.Handle ?? VkPipeline.Null,
                            BasePipelineIndex = _basePipelineIndex
                        };
                        
                        VkPipeline result = VkPipeline.Null;
                        VkException.Check(VkDevice.vkCreateGraphicsPipelines(_pass.Device.Handle, VkPipelineCache.Null,
                            1,
                            &info, _pass.Instance.AllocationCallbacks, &result));
                        Debug.Assert(result != VkPipeline.Null);
                        return new Pipeline(_pass.Device, VkPipelineBindPoint.Graphics, result);
                    }
                }
            }
            finally
            {
                foreach (var pin in _pins)
                    pin.Free();
                foreach (var strPin in _strPins)
                    Marshal.FreeHGlobal(strPin);
            }
        }
    }
}