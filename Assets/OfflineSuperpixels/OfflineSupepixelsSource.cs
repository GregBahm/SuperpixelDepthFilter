using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OfflineSupepixelsSource : MonoBehaviour
{
    private const string RawDigitalPath = @"E:\SuperpixelDepthFilter\Frames\DigitalContent\DigitalContent";
    private const string RawCameraPath = @"E:\SuperpixelDepthFilter\Frames\RawCamera\RawCamera";
    private const string AlphaPath = @"E:\SuperpixelDepthFilter\Frames\RawAlpha\RawAlpha";

    public Material RawDigitalMat;
    public Material RawCameraMat;
    public Material AlphaMat;

    private int _lastFrame;
    [Range(0, 4097)]
    public int Frame;

    private Texture2D _rawDigitalTexture;
    public Texture2D RawDigitalTexture { get { return _rawDigitalTexture; } }
    private Texture2D _rawCameraTexture;
    public Texture2D RawCameraTexture { get { return _rawCameraTexture; } }
    private Texture2D _alphaTexture;
    public Texture2D AlphaTexture { get { return _alphaTexture; } }

    private void Awake()
    {
        _rawDigitalTexture = new Texture2D(540, 400);
        _rawCameraTexture = new Texture2D(540, 400);
        _alphaTexture = new Texture2D(540, 400);
        LoadFrame();
    }

    private void Update()
    {
        RawDigitalMat.SetTexture("_MainTex", _rawDigitalTexture);
        RawCameraMat.SetTexture("_MainTex", _rawCameraTexture);
        AlphaMat.SetTexture("_MainTex", _alphaTexture);

        if (Frame != _lastFrame)
        {
            LoadFrame();
        }
        _lastFrame = Frame;
    }

    private void LoadFrame()
    {
        LoadTextureData(_rawDigitalTexture, RawDigitalPath);
        LoadTextureData(_rawCameraTexture, RawCameraPath);
        LoadTextureData(_alphaTexture, AlphaPath);
    }

    private void LoadTextureData(Texture2D texture, string pathStart)
    {
        string framePath = GetFramePath(pathStart);
        byte[] data = File.ReadAllBytes(framePath);
        texture.LoadImage(data);
    }

    private string GetFramePath(string path)
    {
        return path + Frame.ToString("0000") + ".png";
    }
}
