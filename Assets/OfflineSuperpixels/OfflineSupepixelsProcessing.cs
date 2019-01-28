using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(OfflineSupepixelsSource))]
public class OfflineSupepixelsProcessing : MonoBehaviour
{
    private OfflineSupepixelsSource _source;
    public Material OutputMat;
    
    private int _sourceImageWidth;
    private int _sourceImageHeight;

    private int SourceImageResolution { get { return _sourceImageWidth * _sourceImageHeight; } }

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

    private ComputeBuffer _cellAlphasBuffer;
    private const int CellAlphasStride = sizeof(uint) // Pixels in Cell
        + sizeof(uint); // Alpha in cell

    [Range(0, 1)]
    public float Outlines;

    [Range(0, 1)]
    public float CellAlpha;

    [Range(0, 1)]
    public float RawAlpha;

    [Range(0, 1)]
    public float Impressionist;


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

    void Start()
    {
        _source = GetComponent<OfflineSupepixelsSource>();
        
        _sourceImageWidth = _source.RawCameraTexture.width;
        _sourceImageHeight = _source.RawCameraTexture.height;

        _clearDepthKernel = Compute.FindKernel("ClearDepthData");
        _cellSetupKernel = Compute.FindKernel("CellSetup");
        _pixelAssignmentKernel = Compute.FindKernel("PixelAssignment");

        _cellsAroundPixelBuffer = GetCellsAroundPixelBuffer();
        _cellStartingPoints = GetCellStartingPoints();
        _cellBasisBuffer = new ComputeBuffer(CellCount, CellBasisStride);
        _resultsBuffer = new ComputeBuffer(SourceImageResolution, ResultsBufferStride);
        _cellAlphasBuffer = new ComputeBuffer(CellCount, CellAlphasStride);
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

        OutputMat.SetBuffer("_CellBasisBuffer", _cellBasisBuffer);
        OutputMat.SetBuffer("_ResultsBuffer", _resultsBuffer);
        OutputMat.SetInt("_SourceImageWidth", _sourceImageWidth);
        OutputMat.SetInt("_SourceImageHeight", _sourceImageHeight);
        OutputMat.SetInt("_SuperpixelResolution", SuperpixelResolution);
        OutputMat.SetTexture("_SourceTexture", _source.RawCameraTexture);
        OutputMat.SetTexture("_DigitalTexture", _source.RawDigitalTexture);
        OutputMat.SetTexture("_AlphaTexture", _source.AlphaTexture);
        OutputMat.SetBuffer("_CellAlphasBuffer", _cellAlphasBuffer);
        OutputMat.SetFloat("_Outlines", Outlines);
        OutputMat.SetFloat("_CellAlpha", CellAlpha);
        OutputMat.SetFloat("_RawAlpha", RawAlpha);
        OutputMat.SetFloat("_Impressionist", Impressionist);
    }

    private void DoClearDepthBuffer()
    {
        Compute.SetBuffer(_clearDepthKernel, "_CellAlphasBuffer", _cellAlphasBuffer);
        int cellThreads = Mathf.CeilToInt(CellCount / GroupSize);
        Compute.Dispatch(_clearDepthKernel, cellThreads, 1, 1);
    }

    private void DoCellSetup()
    {
        Compute.SetInt("_SourceImageWidth", _sourceImageWidth);
        Compute.SetInt("_SourceImageHeight", _sourceImageHeight);
        Compute.SetBuffer(_cellSetupKernel, "_CellStartingPoints", _cellStartingPoints);
        Compute.SetBuffer(_cellSetupKernel, "_CellBasisBuffer", _cellBasisBuffer);
        Compute.SetTexture(_cellSetupKernel, "SourceImage", _source.RawCameraTexture);

        int cellThreads = Mathf.CeilToInt(CellCount / GroupSize);
        Compute.Dispatch(_cellSetupKernel, cellThreads, 1, 1);
    }

    private void DoPixelAssignment()
    {
        Compute.SetInt("_SourceImageWidth", _sourceImageWidth);
        Compute.SetInt("_SourceImageHeight", _sourceImageHeight);
        Compute.SetTexture(_pixelAssignmentKernel, "SourceImage", _source.RawCameraTexture);
        Compute.SetTexture(_pixelAssignmentKernel, "AlphaImage", _source.AlphaTexture);
        Compute.SetBuffer(_pixelAssignmentKernel, "_CellsAroundPixelBuffer", _cellsAroundPixelBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_CellBasisBuffer", _cellBasisBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_ResultsBuffer", _resultsBuffer);
        Compute.SetBuffer(_pixelAssignmentKernel, "_CellAlphasBuffer", _cellAlphasBuffer);

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
        datum.UpLeft = GetCellIndex(x, y, -1, 1);
        datum.Left = GetCellIndex(x, y, -1, 0);
        datum.DownLeft = GetCellIndex(x, y, -1, -1);
        datum.Center = GetCellIndex(x, y, 0, 0);
        datum.Up = GetCellIndex(x, y, 0, 1);
        datum.Down = GetCellIndex(x, y, 0, -1);
        datum.UpRight = GetCellIndex(x, y, 1, 1);
        datum.Right = GetCellIndex(x, y, 1, 0);
        datum.DownRight = GetCellIndex(x, y, 1, -1);
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
        _cellAlphasBuffer.Release();
    }
}
