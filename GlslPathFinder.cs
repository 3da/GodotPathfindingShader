using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Range = Godot.Range;

namespace GodotPathfindingShader
{
    public class GlslPathFinder : IPathFinder
    {
        private MapData _mapData;
        private readonly int _maxPathsToFind;
        private readonly int _resultBufferLength;
        private readonly int _maxIterations;
        private readonly RenderingDevice _rd;
        private readonly Rid _waveShader;
        private readonly Rid _wavePipeline;
        private Rid _mapBuffer;
        private readonly Rid _sizeBuffer;
        private Rid _waveUniformSet;
        private Rid _waveBuffer;
        private readonly Rid _pairsBuffer;
        private readonly Rid _pathFindShader;
        private readonly Rid _pathFindPipeline;
        private readonly Rid _resultBuffer;
        private Rid _pathFindUniformSet;
        private uint[,,] _waveMap;
        private readonly PathRequest[] _pathRequests;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PathRequest
        {
            public Vector2I A;
            public Vector2I B;
            //public int Ax;
            //public int Ay;
            //public int Bx;
            //public int By;
            public uint Completed;
            public uint Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PathData
        {
            public uint Count;
            public uint Reserve;
        }

        public GlslPathFinder(int maxPathsToFind = 1, int resultBufferLength = 1024 * 4, int maxIterations = 10000)
        {

            _maxPathsToFind = maxPathsToFind;
            _resultBufferLength = resultBufferLength;
            _maxIterations = maxIterations;
            _rd = RenderingServer.CreateLocalRenderingDevice();

            var shaderFile = GD.Load<RDShaderFile>("res://Wave.glsl");
            var shaderBytecode = shaderFile.GetSpirV();
            _waveShader = _rd.ShaderCreateFromSpirV(shaderBytecode);
            _wavePipeline = _rd.ComputePipelineCreate(_waveShader);

            shaderFile = GD.Load<RDShaderFile>("res://PathFind.glsl");
            shaderBytecode = shaderFile.GetSpirV();
            _pathFindShader = _rd.ShaderCreateFromSpirV(shaderBytecode);
            _pathFindPipeline = _rd.ComputePipelineCreate(_pathFindShader);

            _sizeBuffer = _rd.StorageBufferCreate((uint)(sizeof(int) * 2));

            var pathBufferSize = (uint)(Marshal.SizeOf<PathRequest>() * maxPathsToFind + Marshal.SizeOf<PathData>());
            _pairsBuffer = _rd.StorageBufferCreate(pathBufferSize);

            _resultBuffer = _rd.StorageBufferCreate((uint)(Marshal.SizeOf<Vector2I>() * _resultBufferLength));

            _pathRequests = new PathRequest[_maxPathsToFind];
        }

        void CreateUniforms()
        {
            if (_waveUniformSet.IsValid)
                _rd.FreeRid(_waveUniformSet);
            if (_pathFindUniformSet.IsValid)
                _rd.FreeRid(_pathFindUniformSet);

            _waveUniformSet = _rd.UniformSetCreate([new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0,
                _Ids = [_mapBuffer]
            }, new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1,
                _Ids = [_sizeBuffer]
            }, new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 2,
                _Ids = [_waveBuffer]
            }, new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 3,
                _Ids = [_pairsBuffer]
            }], _waveShader, 0);

            _pathFindUniformSet = _rd.UniformSetCreate([new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 0,
                _Ids = [_sizeBuffer]
            }, new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 1,
                _Ids = [_waveBuffer]
            }, new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 2,
                _Ids = [_pairsBuffer]
            }, new RDUniform
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 3,
                _Ids = [_resultBuffer]
            }], _pathFindShader, 0);
        }

        public void FindPaths(IList<PathFindItem> items)
        {
            if (!_mapBuffer.IsValid)
                return;

            var pathRequests = _pathRequests;
            for (int i = 0; i < items.Count; i++)
            {
                var q = items[i];
                pathRequests[i] = new PathRequest()
                {
                    A = q.A,
                    B = q.B
                };
            }

            var pathData = new PathData()
            {
                Count = (uint)items.Count
            };
            var pathDataBytes = new[] { pathData }.AsSpan().AsBytes();

            var pathBytes = pathRequests.AsSpan().AsBytes();
            _rd.BufferUpdate(_pairsBuffer, 0, (uint)pathDataBytes.Length, pathDataBytes);
            _rd.BufferUpdate(_pairsBuffer, (uint)pathDataBytes.Length, (uint)pathBytes.Length, pathBytes);

            uint xGroups = (uint)((_mapData.Width - 1) / 16 + 1);
            uint yGroups = (uint)((_mapData.Height - 1) / 16 + 1);
            uint zGroups = (uint)(items.Count);

            var sw = Stopwatch.StartNew();

            var computeList = _rd.ComputeListBegin();

            int[] pushConstants = [0, 0, 0, 0];
            var pushBytes = pushConstants.AsSpan().AsBytes();

            for (int iteration = 0; iteration < _maxIterations; iteration++)
            {
                pushConstants[0] = iteration;
                _rd.ComputeListBindComputePipeline(computeList, _wavePipeline);
                _rd.ComputeListSetPushConstant(computeList, pushBytes, (uint)pushBytes.Length);
                _rd.ComputeListBindUniformSet(computeList, _waveUniformSet, 0);
                _rd.ComputeListDispatch(computeList, xGroups: xGroups, yGroups: yGroups, zGroups: zGroups);
                _rd.ComputeListAddBarrier(computeList);
            }

            _rd.ComputeListBindComputePipeline(computeList, _pathFindPipeline);
            _rd.ComputeListBindUniformSet(computeList, _pathFindUniformSet, 0);
            _rd.ComputeListDispatch(computeList, xGroups: 1, yGroups: 1, zGroups: zGroups);
            _rd.ComputeListAddBarrier(computeList);

            _rd.ComputeListEnd();

            _rd.Submit();
            _rd.Sync();

            sw.Stop();

            _rd.BufferGetData(_pairsBuffer, (uint)pathDataBytes.Length, (uint)pathBytes.Length).CopyTo(pathBytes);

            var resultVectors = new Vector2I[_resultBufferLength];
            var resultBytes = resultVectors.AsSpan().AsBytes();

            _rd.BufferGetData(_resultBuffer).CopyTo(resultBytes);


            int indexStart = 0;
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var pathRequest = pathRequests[index];
                var range = new System.Range(indexStart, indexStart + (int)pathRequest.Completed);
                if (range.End.Value > resultVectors.Length)
                    return;
                indexStart += (int)pathRequest.Completed;
                item.Path = resultVectors[range];
            }
        }

        public void SetMap(MapData mapData)
        {
            bool sizeChanged = mapData.Width != _mapData?.Width || mapData.Height != _mapData?.Height;

            var totalMapSize = (uint)mapData.Cells.Length * sizeof(bool);

            if (sizeChanged)
            {
                if (_mapBuffer.IsValid)
                {
                    _rd.FreeRid(_mapBuffer);
                }

                if (_waveBuffer.IsValid)
                {
                    _rd.FreeRid(_waveBuffer);
                }

                _mapBuffer = _rd.StorageBufferCreate(totalMapSize);
                _waveBuffer = _rd.StorageBufferCreate((uint)(mapData.Cells.Length * sizeof(uint) * _maxPathsToFind));

                CreateUniforms();

                _waveMap = new uint[_maxPathsToFind, mapData.Height, mapData.Width];
            }

            _mapData = mapData;

            var mapBytes = MemoryMarshal.AsBytes(mapData.Cells.AsSpan());
            _rd.BufferUpdate(_mapBuffer, 0, totalMapSize, mapBytes);
            _rd.BufferUpdate(_sizeBuffer, 0, sizeof(int) * 2,
                BitConverter.GetBytes(mapData.Width).Concat(BitConverter.GetBytes(mapData.Height)).ToArray());
        }
    }
}
