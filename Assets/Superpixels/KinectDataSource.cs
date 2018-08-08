using UnityEngine;
using System.Collections;
using Windows.Kinect;
using System.Runtime.InteropServices;
using System;

public class KinectDataSource : MonoBehaviour
{
	private KinectSensor _kinectSensor;
	private CoordinateMapper _coordinateMapper;
	private MultiSourceFrameReader _multiSourceFrameReader;
	private DepthSpacePoint[] _depthCoordinates;

	private byte[] _colorBuffer;

	private ushort[] _depthBuffer;

	const int        cDepthWidth  = 512;
	const int        cDepthHeight = 424;
	const int        cColorWidth  = 1920;
	const int        cColorHeight = 1080;

    public ushort[] GetData()
    {
        return _depthBuffer;
    }

    private long frameCount = 0;

	private double elapsedCounter = 0.0;
	private double fps = 0.0;
	
	private Texture2D _colorRGBX;

    private bool nullFrame = false;

	void Awake()
	{
		_colorBuffer = new byte[cColorWidth * cColorHeight * 4];
		_depthBuffer = new ushort[cDepthWidth * cDepthHeight];

		_colorRGBX = new Texture2D (cColorWidth, cColorHeight, TextureFormat.RGBA32, false);

        _depthCoordinates = new DepthSpacePoint[cColorWidth * cColorHeight];

		InitializeDefaultSensor ();
	}

	Rect fpsRect = new Rect(10, 10, 200, 30);
	Rect nullFrameRect = new Rect(10, 50, 200, 30);

	void OnGUI () 
	{
		GUI.Box (fpsRect, "FPS: " + fps.ToString("0.00"));

		if (nullFrame)
		{
			GUI.Box (nullFrameRect, "NULL MSFR Frame");
		}
	}

	public Texture2D GetColorTexture()
	{
		return _colorRGBX;
	}

	public DepthSpacePoint[] GetDepthCoordinates()
	{
		return _depthCoordinates;
	}

	void InitializeDefaultSensor()
	{	
		_kinectSensor = KinectSensor.GetDefault();
		
		if (_kinectSensor != null)
		{
			// Initialize the Kinect and get coordinate mapper and the frame reader
			_coordinateMapper = _kinectSensor.CoordinateMapper;
			
			_kinectSensor.Open();
			if (_kinectSensor.IsOpen)
			{
				_multiSourceFrameReader = _kinectSensor.OpenMultiSourceFrameReader(
					FrameSourceTypes.Color | FrameSourceTypes.Depth);
			}
		}
		
		if (_kinectSensor == null)
		{
			UnityEngine.Debug.LogError("No ready Kinect found!");
		}
	}

	void ProcessFrame()
	{
		var pDepthData = GCHandle.Alloc(_depthBuffer, GCHandleType.Pinned);
		var pDepthCoordinatesData = GCHandle.Alloc(_depthCoordinates, GCHandleType.Pinned);

		_coordinateMapper.MapColorFrameToDepthSpaceUsingIntPtr(
			pDepthData.AddrOfPinnedObject(), 
			(uint)_depthBuffer.Length * sizeof(ushort),
			pDepthCoordinatesData.AddrOfPinnedObject(), 
			(uint)_depthCoordinates.Length);

		pDepthCoordinatesData.Free();
		pDepthData.Free();

		_colorRGBX.LoadRawTextureData(_colorBuffer);
		_colorRGBX.Apply ();
	}
	
	void Update()
	{
		// Get FPS
		elapsedCounter+=Time.deltaTime;
		if(elapsedCounter > 1.0)
		{
			fps = frameCount / elapsedCounter;
			frameCount = 0;
			elapsedCounter = 0.0;
		}

		if (_multiSourceFrameReader == null) 
		{
			return;
		}

		var pMultiSourceFrame = _multiSourceFrameReader.AcquireLatestFrame();
		if (pMultiSourceFrame != null) 
		{
			frameCount++;
			nullFrame = false;

			using(var pDepthFrame = pMultiSourceFrame.DepthFrameReference.AcquireFrame())
			{
				using(var pColorFrame = pMultiSourceFrame.ColorFrameReference.AcquireFrame())
				{
					using(var pBodyIndexFrame = pMultiSourceFrame.BodyIndexFrameReference.AcquireFrame())
					{
						// Get Depth Frame Data.
						if (pDepthFrame != null)
						{
							var pDepthData = GCHandle.Alloc (_depthBuffer, GCHandleType.Pinned);
							pDepthFrame.CopyFrameDataToIntPtr(pDepthData.AddrOfPinnedObject(), (uint)_depthBuffer.Length * sizeof(ushort));
							pDepthData.Free();
						}
						
						// Get Color Frame Data
						if (pColorFrame != null)
						{
							var pColorData = GCHandle.Alloc (_colorBuffer, GCHandleType.Pinned);
							pColorFrame.CopyConvertedFrameDataToIntPtr(pColorData.AddrOfPinnedObject(), (uint)_colorBuffer.Length, ColorImageFormat.Rgba);
                            pColorData.Free();
                        }
					}
				}
			}

			ProcessFrame();
        }
        else
		{
			nullFrame = true;
		}
	}

	void OnApplicationQuit()
	{
		_depthBuffer = null;
		_colorBuffer = null;

		if (_depthCoordinates != null)
		{
			_depthCoordinates = null;
		}

		if (_multiSourceFrameReader != null)
		{
			_multiSourceFrameReader.Dispose();
			_multiSourceFrameReader = null;
		}
		
		if (_kinectSensor != null)
		{
			_kinectSensor.Close();
			_kinectSensor = null;
		}
	}
}

