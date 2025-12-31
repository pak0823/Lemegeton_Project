using System;
using UnityEngine;

// 1. 좌표 및 크기용 기본 구조체
[Serializable]
public struct AsepriteRect
{
    public int x;
    public int y;
    public int w;
    public int h;
}

[Serializable]
public struct AsepriteSize
{
    public int w;
    public int h;
}

// 2. 개별 프레임 데이터 (Array 내부 항목)
[Serializable]
public class AsepriteFrameData
{
    public string filename; // Hash때와 달리 파일명이 여기로 들어옵니다.
    public AsepriteRect frame;
    public bool rotated;
    public bool trimmed;
    public AsepriteRect spriteSourceSize;
    public AsepriteSize sourceSize;
    public int duration;
}

// 3. 메타 데이터
[Serializable]
public class AsepriteMeta
{
    public string app;
    public string version;
    public AsepriteSize size;
    public string scale;
}

// 4. 최상위 루트 (JSON 전체)
[Serializable]
public class AsepriteArrayRoot
{
    // Hash 때는 Dictionary였지만, 이제는 배열([])입니다.
    public AsepriteFrameData[] frames;
    public AsepriteMeta meta;
}