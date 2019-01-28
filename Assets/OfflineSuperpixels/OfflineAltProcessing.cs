using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(OfflineSupepixelsSource))]
public class OfflineAltProcessing : MonoBehaviour
{
    private OfflineSupepixelsSource _source;
    public Material OutputMat;

    void Start()
    {
        _source = GetComponent<OfflineSupepixelsSource>();
    }

    void Update()
    {
        OutputMat.SetFloat("_SourceImageWidth", _source.RawCameraTexture.width);
        OutputMat.SetFloat("_SourceImageHeight", _source.RawCameraTexture.height);

        OutputMat.SetTexture("_SourceTexture", _source.RawCameraTexture);
        OutputMat.SetTexture("_DigitalTexture", _source.RawDigitalTexture);
        OutputMat.SetTexture("_AlphaTexture", _source.AlphaTexture);
    }
}
