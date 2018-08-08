using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Windows.Kinect;

[RequireComponent(typeof(KinectDataSource))]
public class SuperPixelManager : MonoBehaviour
{
    private KinectDataSource _kinectDataSource;
    public Material Mat;
    private int _sourceImageWidth;
    private int _sourceImageHeight;
    private Texture2D _sourceTexture;

    private int SourceImageResolution { get { return _sourceImageWidth * _sourceImageHeight; } }
    private int DepthImageResolution { get { return 512 * 424; } }
    [Range(0, 64)]
    public int SuperpixelResolution = 16;
    private int CellCount { get { return SuperpixelResolution * SuperpixelResolution; } }
    public ComputeShader Compute;

    private int _cellSetupKernel;
    private int _pixelAssignmentKernel;
    private int _clearDepthKernel;
    private const int GroupSize = 64;

    private ComputeBuffer _cellsAroundPixelBuffer;
    private const int CellsAroundPixelStride = sizeof(int) * 9;

    private ComputeBuffer _cellStartingPoints;
    private const int CellStartingPointsStride = sizeof(float) * 2;

    private ComputeBuffer _cellBasisBuffer;
    private const int CellBasisStride = sizeof(float) * 3 // Color
        + sizeof(float) * 2; // X and Y

    private ComputeBuffer _resultsBuffer;
    private const int ResultsBufferStride = sizeof(int);

    private ComputeBuffer _depthDataBuffer;
    private const int DepthBufferStride = sizeof(int);
    
    public ComputeBuffer _depthMappingBuffer;
    private const int DepthMappingStride = sizeof(float) * 2;
    DepthSpacePoint[] _depthPoints;

    public ComputeBuffer _cellDepthsBuffer;
    private const int CellDepthsStride = sizeof(int) // Total Depth
        + sizeof(int); // Pixels in Cell

    struct CellsAroundPixel
    {
        public int Center;
        public int UpLeft;
        public int Up;
        public int UpRight;
        public int Right;
        public int Left;
        public int DownLeft;
        public int Down;
        public int DownRight;
    }
    
    struct CellBasisData
    {
        public Vector3 Color;
        public Vector2 Pos;
    }

    struct CellDepthData
    {
        public int TotalDepth;
        public int PixelsInCell;
    }

	void Start ()
    {
        _kinectDataSource = GetComponent<KinectDataSource>();
        _sourceTexture = _kinectDataSource.GetColorTexture();
        _sourceImageWidth = _sourceTexture.width;
        _sourceImageHeight = _sourceTexture.height;
        _depthPoints = _kinectDataSource.GetDepthCoordinates();

        _clearDepthKernel = Compute.FindKernel("ClearDepthData");
        _cellSetupKernel = Compute.FindKernel("CellSetup");
        _pixelAssignmentKernel = Compute.FindKernel("PixelAssignment");

        _cellsAroundPixelBuffer = GetCellsAroundPixelBuffer();
        _cellStartingPoints = GetCellStartingPoints();
        _cellBasisBuffer = new ComputeBuffer(CellCount, CellBasisStride);
        _resultsBuffer = new ComputeBuffer(SourceImageResolution, ResultsBufferStride);
        _depthDataBuffer = new ComputeBuffer(DepthImageResolution, DepthBufferStride);
        _depthMappingBuffer = new ComputeBuffer(_depthPoints.Length, DepthMappingStride);
        _cellDepthsBuffer = new ComputeBuffer(CellCount, CellDepthsStride);
    }

    private ComputeBuffer GetCellStartingPoints()
    {
        ComputeBuffer ret = new ComputeBuffer(CellCount, CellStartingPointsStride);
        Vector2[] data = new Vector2[CellCount];
        for (int x = 0; x < SuperpixelResolution; x++)
        {
            for (int y = 0; y < SuperpixelResolution; y++)
            {
                float retX = (float)x / SuperpixelResolution;
                float retY = (float)y / SuperpixelResolution;
                int index = x + y * SuperpixelResolution;
                data[index] = new Vector2(retX, retY);
            }
        }
        ret.SetData(data);
        return ret;
    }

    void Update()
    {
        DoClearDepthBuffer();
        DoCellSetup();
        DoPixelAssignment();

        _depthMappingBuffer.SetData(_depthPoints);
        _depthDataBuffer.SetData(Array.ConvertAll(_kinectDataSource.GetData(), Convert.ToInt32));

        Mat.SetBuffer("_CellBasisBuffer", _cellBasisBuffer);
        Mat.SetBuffer("_ResultsBuffer", _resultsBuffer);
        Mat.SetBuffer("_DepthData", _depthDataBuffer);
        Mat.SetInt("_SourceImageWidth", _sourceImageWidth);
        Mat.SetInt("_SourceImageHeight", _sourceImageHeight);
        Mat.SetInt("_SuperpixelResolution", SuperpixelResolution);
        Mat.SetTexture("_SourceTexture", _sourceTexture);
        Mat.SetBuffer("_DepthMappings", _depthMappingBuffer);
        Mat.SetBuffer("_CellDepthsBuffer", _cellDepthsBuffer);
    }

    private void DoClearDepthBuffer()
    {
        Compute.SetBuffer(_clearDepthKernel, "_CellDepthsBuffer", _cellDepthsBuffer);
        int cellThreads = Mathf.CeilToInt(CellCount / GroupSize);
        Compute.Dispatch(_clearDepthKernel, cellThreads, 1, 1);
    }

    private void DoCellSetup()
    {
        Compute.SetInt("_SourceImageWidth", _sourceImageWidth);
        Compute.SetInt("_SourceImageHeight", _sourceImageHeight);
        Compute.SetBuffer(_cellSetupKernel, "_CellStartingPoints", _cellStartingPoints);
        Compute.SetBuffer(_cellSetupKernel, "_CellBasisBuffer", _cellBasisBuffer);
        Compute.SetTexture(_cellSetupKernel, "SourceImage", _sourceTexture);

        int cellThreads = Mathf.CeilToInt(CellCount / GroupSize);
        Compute.Dispatch(_cellSetupKernel, cellThreads, 1, 1);
    }

    private void DoPixelAssignment()
    {
        Compute.SetInt("_SourceImageWidth", _sourceImageWidth);
        Compute.SetInt("_SourceImageHeight", _sourceImageHeight);
        Compute.SetTexture(_pixelAssignmentKernel, "SourceImage", _sourceTexture);
        Compute.SetBuffer(_pixelAssignmentKernel, "_CellsAroundPixelBuffer", _cellsAroundPixelBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_CellBasisBuffer", _cellBasisBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_ResultsBuffer", _resultsBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_DepthMappings", _depthMappingBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_DepthData", _depthDataBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_CellDepthsBuffer", _cellDepthsBuffer);

        int pixelAssignmentThreads = Mathf.CeilToInt(SourceImageResolution / GroupSize);
        Compute.Dispatch(_pixelAssignmentKernel, pixelAssignmentThreads, 1, 1);
    }

    private ComputeBuffer GetCellsAroundPixelBuffer()
    {
        ComputeBuffer ret = new ComputeBuffer(SourceImageResolution, CellsAroundPixelStride);
        CellsAroundPixel[] data = new CellsAroundPixel[SourceImageResolution];
        for (int x = 0; x < _sourceImageWidth; x++)
        {
            for (int y = 0; y < _sourceImageHeight; y++)
            {
                CellsAroundPixel datum = GetCellsAroundPixel(x, y);
                int index = x + y * _sourceImageWidth;
                data[index] = datum;
            }
        }
        ret.SetData(data);
        return ret;
    }

    private CellsAroundPixel GetCellsAroundPixel(int x, int y)
    {
        CellsAroundPixel datum = new CellsAroundPixel();
        datum.UpLeft =      GetCellIndex(x, y, -1,  1);
        datum.Left =        GetCellIndex(x, y, -1,  0);
        datum.DownLeft =    GetCellIndex(x, y, -1, -1);
        datum.Center =      GetCellIndex(x, y,  0,  0);
        datum.Up =          GetCellIndex(x, y,  0,  1);
        datum.Down =        GetCellIndex(x, y,  0, -1);
        datum.UpRight =     GetCellIndex(x, y,  1,  1);
        datum.Right =       GetCellIndex(x, y,  1,  0);
        datum.DownRight =   GetCellIndex(x, y,  1, -1);
        return datum;
    }

    private int GetCellIndex(int x, int y, int xOffset, int yOffset)
    {
        float xPercent = (float)x / _sourceImageWidth;
        float yPercent = (float)y / _sourceImageHeight;

        int centerXCell = Mathf.RoundToInt(xPercent * SuperpixelResolution);
        int centerYCell = Mathf.RoundToInt(yPercent * SuperpixelResolution);

        int cellX = centerXCell + xOffset;
        int cellY = centerYCell + yOffset;

        cellX = Mathf.Clamp(cellX, 0, SuperpixelResolution);
        cellY = Mathf.Clamp(cellY, 0, SuperpixelResolution);
        return cellX + cellY * SuperpixelResolution;
    }

    private void OnDestroy()
    {
        _cellBasisBuffer.Release();
        _cellStartingPoints.Release();
        _cellsAroundPixelBuffer.Release();
        _depthMappingBuffer.Release();
        _cellDepthsBuffer.Release();
    }
}