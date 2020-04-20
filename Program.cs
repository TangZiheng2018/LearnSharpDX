﻿using SharpDX.Windows;
using System;
using SharpDX.DXGI;
using SharpDX.D3DCompiler;
using D3D11 = SharpDX.Direct3D11;
using SharpDX.Direct3D11;
using SharpDX;

namespace deleteSharpDX {
    class Program {
        static void Main(string[] args) {
            using (var temp = new MySharpDXForm()) {
            }
        }
    }
    class MySharpDXForm : IDisposable {
        private RenderForm _renderForm;
        private Matrix proj;
        private Matrix view;
        private Matrix world;
        private Vector3 targetViewDir;   //摄像机目标向量
        private bool _resized;
        private D3D11.Device _d3DDevice;
        private D3D11.DeviceContext _d3DDeviceContext;
        private SwapChain _swapChain;
        private InputLayout _inputLayout;
        private ShaderSignature _inputShaderSignature;
        private VertexShader _vertexShader;
        private PixelShader _pixelShader;
        RasterizerStateDescription rasterizerStateDescWire;
        RasterizerStateDescription rasterizerStateDescSolid;
        Vector3 camPos  = new Vector3(0.0f, 2.0f, -5.0f);
        Vector3 camTo   = new Vector3(2f, 0f, 0f);
        Vector3 camUp   = new Vector3(0f, 1f, 0f);
        struct MyFuncVertex {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 TangentU;
            public Vector2 TexC;
            public MyFuncVertex(Vector3 pos, Vector3 nor, Vector3 tan, Vector2 tex) {
                Position = pos;
                Normal = nor;
                TangentU = tan;
                TexC = tex;
            }
        }
        struct MeshData {
            public MyFuncVertex[] Vertices;
            public int[] Indices;
            public MeshData(int xNum, int zNum) {
                Vertices = new MyFuncVertex[xNum * zNum];
                Indices = new int[(xNum - 1) * (zNum - 1) * 6];
            }
        }
        private D3D11.InputElement[] _inputElementsForMesh = new D3D11.InputElement[] {
                new D3D11.InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0),
                new D3D11.InputElement("NORMAL", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TANGENTU", 0, Format.R32G32B32_Float, 0),
                new D3D11.InputElement("TEXC", 0, Format.R32G32_Float, 0),
            };
        private Matrix worldViewProj;
        private System.Drawing.Point preMouse;
        private int dx;
        private int dy;
        private float targetX = 0f;
        private float targetY = 0f;
        private float targetZ = 5f;
        private bool pitchFlag;
        MeshData meshData;
        /// <summary>
        /// 初始化
        /// </summary>
        public MySharpDXForm() {
            GenerateFXY(5f, 5f, 10, 10);
            GenerateMesh(10, 10);
            _renderForm = new RenderForm();
            _renderForm.KeyDown += _renderForm_KeyDown;
            _renderForm.ClientSize = new System.Drawing.Size(_renderForm.ClientSize.Width, _renderForm.ClientSize.Height);
            _renderForm.Text = "愉快的学习SharpDX";
            _renderForm.Icon = null;
            _renderForm.ResizeBegin += (object sender, EventArgs e) => { _resized = true; };
            _renderForm.MouseDown += _renderForm_MouseDown;
            _renderForm.MouseUp += _renderForm_MouseUp;
            _renderForm.MouseMove += _renderForm_MouseMove;
            ModeDescription backBufferDesc = new ModeDescription(_renderForm.ClientSize.Width, _renderForm.ClientSize.Height, new Rational(60, 1), Format.R8G8B8A8_UNorm);
            SwapChainDescription swapChainDesc = new SwapChainDescription() {
                ModeDescription = backBufferDesc,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = _renderForm.Handle,
                IsWindowed = true,
                Flags = SwapChainFlags.AllowModeSwitch,
                SwapEffect = SwapEffect.Discard,
            };
            D3D11.Device.CreateWithSwapChain(
                SharpDX.Direct3D.DriverType.Hardware,
                D3D11.DeviceCreationFlags.Debug,
                swapChainDesc, out _d3DDevice, out _swapChain);
            _d3DDeviceContext = _d3DDevice.ImmediateContext;
            var factory = _swapChain.GetParent<Factory>();
            factory.MakeWindowAssociation(_renderForm.Handle, WindowAssociationFlags.IgnoreAll);
            #region 栅格化显示线框/固体state的初始化
            rasterizerStateDescWire = new RasterizerStateDescription() {
                FillMode = FillMode.Wireframe,
                CullMode = CullMode.None,
            };
            rasterizerStateDescSolid = new RasterizerStateDescription() {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
            };
            #endregion
            _d3DDeviceContext.Rasterizer.State = new RasterizerState(_d3DDevice, rasterizerStateDescWire);
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("../../MyShader.fx", "VS", "vs_4_0")) {
                _inputShaderSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                _vertexShader = new D3D11.VertexShader(_d3DDevice, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("../../MyShader.fx", "PS", "ps_4_0")) {
                _pixelShader = new D3D11.PixelShader(_d3DDevice, pixelShaderByteCode);
            }
            _inputLayout = new D3D11.InputLayout(_d3DDevice, _inputShaderSignature, _inputElementsForMesh);
            var FxyVertexBuffer = D3D11.Buffer.Create<MyFuncVertex>(_d3DDevice, BindFlags.VertexBuffer, meshData.Vertices);
            var FxyConstBuffer = new D3D11.Buffer(_d3DDevice, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            var indexBufferDesc = new BufferDescription(Utilities.SizeOf<int>() * meshData.Indices.Length, ResourceUsage.Default, BindFlags.IndexBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);
            var FxyIndexBuffer = D3D11.Buffer.Create<int>(_d3DDevice, meshData.Indices, indexBufferDesc);
            _d3DDeviceContext.InputAssembler.InputLayout = _inputLayout;
            _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            _d3DDeviceContext.InputAssembler.SetVertexBuffers(0,
                new VertexBufferBinding(FxyVertexBuffer, Utilities.SizeOf<MyFuncVertex>(), 0));
            _d3DDeviceContext.InputAssembler.SetIndexBuffer(FxyIndexBuffer, Format.R32_UInt, 0);
            _d3DDeviceContext.VertexShader.SetConstantBuffer(0, FxyConstBuffer);
            _d3DDeviceContext.VertexShader.Set(_vertexShader);
            _d3DDeviceContext.PixelShader.Set(_pixelShader);
            proj = Matrix.Identity;
            view = Matrix.LookAtLH(camPos, camTo, camUp);
            world = Matrix.Identity;
            _resized = true;

            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;

            RenderLoop.Run(_renderForm, () => {
                targetViewDir = new Vector3(targetX, targetY, targetZ);
                view = Matrix.LookAtLH(camPos, camPos + targetViewDir, camUp);
                if (_resized) {
                    Utilities.Dispose(ref backBuffer);
                    Utilities.Dispose(ref renderView);
                    Utilities.Dispose(ref depthBuffer);
                    Utilities.Dispose(ref depthView);
                    _swapChain.ResizeBuffers(swapChainDesc.BufferCount, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
                    backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0);
                    renderView = new RenderTargetView(_d3DDevice, backBuffer);
                    depthBuffer = new Texture2D(_d3DDevice, new Texture2DDescription() {
                        Format = Format.D32_Float_S8X24_UInt,
                        ArraySize = 1,
                        MipLevels = 1,
                        Width = _renderForm.ClientSize.Width,
                        Height = _renderForm.ClientSize.Height,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.DepthStencil,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None
                    });
                    depthView = new DepthStencilView(_d3DDevice, depthBuffer);
                    _d3DDeviceContext.Rasterizer.SetViewport(new Viewport(0, 0, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height, 0.0f, 1.0f));
                    _d3DDeviceContext.OutputMerger.SetTargets(depthView, renderView);
                    proj = Matrix.PerspectiveFovLH((float)Math.PI / 4f, _renderForm.ClientSize.Width / (float)_renderForm.ClientSize.Height, 0.1f, 100f);
                    _resized = false;
                }
                _d3DDeviceContext.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                _d3DDeviceContext.ClearRenderTargetView(renderView, SharpDX.Color.Black);
                var viewPort = new Viewport(0, 0, _renderForm.ClientSize.Width, _renderForm.ClientSize.Height);
                _d3DDeviceContext.Rasterizer.SetViewport(viewPort);
                var viewProj = Matrix.Multiply(view, proj);
                worldViewProj = world * viewProj;
                worldViewProj.Transpose();

                _d3DDeviceContext.UpdateSubresource(ref worldViewProj, FxyConstBuffer);
                _d3DDeviceContext.DrawIndexed(meshData.Indices.Length, 0, 0);
                _swapChain.Present(0, PresentFlags.None);
            });
        }
        public void Dispose() {
            _swapChain.Dispose();
            _d3DDevice.Dispose();
            _d3DDeviceContext.Dispose();
            _renderForm.Dispose();
        }
        #region Some Unimmportant Methods
        private void _renderForm_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            pitchFlag = false;
            preMouse = e.Location;
        }
        private void _renderForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            pitchFlag = true;
            preMouse = e.Location;
        }
        private void _renderForm_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (pitchFlag) {
                dx = e.Location.X - preMouse.X;
                dy = e.Location.Y - preMouse.Y;
                targetX -= dx / 10000f;
                targetY += dy / 10000f;
            }
        }
        private void _renderForm_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e) {
            switch (e.KeyCode) {
                //按键设置显示方式：S--Solid; W--Wireframe
                case System.Windows.Forms.Keys.Z:
                    _d3DDeviceContext.Rasterizer.State = new RasterizerState(_d3DDevice, rasterizerStateDescWire);
                    break;
                case System.Windows.Forms.Keys.X:
                    _d3DDeviceContext.Rasterizer.State = new RasterizerState(_d3DDevice, rasterizerStateDescSolid);
                    break;
                //元素 P-点 L-线 T-三角形
                case System.Windows.Forms.Keys.P:
                    _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.PointList;
                    break;
                case System.Windows.Forms.Keys.L:
                    _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.LineList;
                    break;
                case System.Windows.Forms.Keys.T:
                    _d3DDeviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
                    break;
                //全屏
                case System.Windows.Forms.Keys.F5:
                    _swapChain.SetFullscreenState(true, null);
                    _resized = true;
                    break;
                case System.Windows.Forms.Keys.F6:
                    _swapChain.SetFullscreenState(false, null);
                    _resized = true;
                    break;
                //移动摄像机：
                case System.Windows.Forms.Keys.W:
                    camPos.Z += 0.1f;
                    break;
                case System.Windows.Forms.Keys.S:
                    camPos.Z -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.A:
                    camPos.X -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.D:
                    camPos.X += 0.1f;
                    break;
                case System.Windows.Forms.Keys.Q:
                    camPos.Y += 0.1f;
                    break;
                case System.Windows.Forms.Keys.E:
                    camPos.Y -= 0.1f;
                    break;
                case System.Windows.Forms.Keys.Escape:
                    _renderForm.Close();
                    break;
                default:
                    break;
            }
        }
        #endregion
        #region 三维函数图
        int[] fxyIndices = new int[] {};

        void GenerateMesh(int xNum, int zNum) {
            int k = 0;
            for (int quadX = 0; quadX < xNum-1; quadX++) {
                for (int quadZ = 0; quadZ < zNum-1; quadZ++) {
                    meshData.Indices[k + 0] = quadX * zNum + quadZ;
                    meshData.Indices[k + 1] = quadX * zNum + quadZ + 1;
                    meshData.Indices[k + 2] = (quadX + 1) * zNum + quadZ + 1;

                    meshData.Indices[k + 3] = (quadX + 1) * zNum + quadZ + 1;
                    meshData.Indices[k + 4] = (quadX + 1) * zNum + quadZ;
                    meshData.Indices[k + 5] = quadX * zNum + quadZ;
                    k += 6;
                }
            }
        }
        void GenerateFXY(float xlength, float zlength, int xNum, int zNum) {
            meshData = new MeshData(xNum, zNum);
            var dx = xlength / xNum;
            var dz = zlength / zNum;
            // for texturing 纹理坐标
            var du = 1f / xNum;
            var dv = 1f / zNum;
            for (int xIndex = 0; xIndex < xNum; xIndex++) {
                for (int zIndex = 0; zIndex < zNum; zIndex++) {
                    // 位置坐标
                    meshData.Vertices[xIndex * zNum + zIndex].Position  = new Vector3(xIndex, 0f, zIndex);
                    // 光照
                    meshData.Vertices[xIndex * zNum + zIndex].Normal    = new Vector3(0f, 1f, 0f);
                    meshData.Vertices[xIndex * zNum + zIndex].TangentU  = new Vector3(1f, 0f, 0f);
                    // 纹理坐标                
                    meshData.Vertices[xIndex * zNum + zIndex].TexC.X    = xIndex * du;
                    meshData.Vertices[xIndex * zNum + zIndex].TexC.Y    = zIndex * dv;
                }
            }
        }
        #endregion
    }
}