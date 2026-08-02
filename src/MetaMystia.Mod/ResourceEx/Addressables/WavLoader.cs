using System;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace MetaMystia.ResourceEx.Addressables;

/// <summary>
/// Minimal 16-bit PCM WAV → <see cref="AudioClip"/> loader, for tests / mod assets.
/// Supports mono / stereo, any sample rate. Throws on non-PCM or non-16-bit files.
/// </summary>
public static class WavLoader
{
    public static AudioClip LoadFromFile(string path, string clipName = null)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"WAV file not found: {path}", path);

        var bytes = File.ReadAllBytes(path);
        return LoadFromBytes(bytes, clipName ?? Path.GetFileNameWithoutExtension(path));
    }

    public static AudioClip LoadFromBytes(byte[] data, string clipName)
    {
        if (data == null || data.Length < 44)
            throw new ArgumentException("Invalid WAV: too short", nameof(data));

        // Validate RIFF header
        if (data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F' ||
            data[8] != 'W' || data[9] != 'A' || data[10] != 'V' || data[11] != 'E')
            throw new InvalidDataException("Not a valid RIFF/WAVE file");

        // Locate "fmt " and "data" chunks (skip optional chunks like LIST/JUNK).
        int pos = 12;
        int fmtChunkPos = -1, fmtChunkSize = 0;
        int dataChunkPos = -1, dataChunkSize = 0;
        while (pos + 8 <= data.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(data, pos, 4);
            int size = BitConverter.ToInt32(data, pos + 4);
            if (id == "fmt ") { fmtChunkPos = pos + 8; fmtChunkSize = size; }
            else if (id == "data") { dataChunkPos = pos + 8; dataChunkSize = size; break; }
            pos += 8 + size + (size & 1); // chunks are word-aligned
        }
        if (fmtChunkPos < 0 || dataChunkPos < 0)
            throw new InvalidDataException("WAV missing fmt or data chunk");

        ushort audioFormat = BitConverter.ToUInt16(data, fmtChunkPos + 0);
        ushort numChannels = BitConverter.ToUInt16(data, fmtChunkPos + 2);
        int sampleRate = BitConverter.ToInt32(data, fmtChunkPos + 4);
        ushort bitsPerSample = BitConverter.ToUInt16(data, fmtChunkPos + 14);

        // Format 1 = PCM int, Format 3 = IEEE float.
        // (Format 0xFFFE WAVE_FORMAT_EXTENSIBLE is not handled here.)
        bool isFloat = audioFormat == 3;
        bool isPcm = audioFormat == 1;
        if (!isPcm && !isFloat)
            throw new NotSupportedException($"Only PCM (1) or IEEE float (3) supported; got format {audioFormat}");
        if (isFloat && bitsPerSample != 32)
            throw new NotSupportedException($"Only 32-bit IEEE float supported; got {bitsPerSample}-bit float");
        if (isPcm && bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
            throw new NotSupportedException($"PCM bit depth must be 8/16/24/32; got {bitsPerSample}-bit");

        int bytesPerSample = bitsPerSample / 8;
        int totalSamples = dataChunkSize / bytesPerSample;
        int samplesPerChannel = totalSamples / numChannels;

        var floats = new float[totalSamples];
        if (isFloat)
        {
            for (int i = 0; i < totalSamples; i++)
                floats[i] = BitConverter.ToSingle(data, dataChunkPos + i * 4);
        }
        else switch (bitsPerSample)
            {
                case 8: // PCM8 is unsigned, biased by 128
                    for (int i = 0; i < totalSamples; i++)
                        floats[i] = (data[dataChunkPos + i] - 128) / 128f;
                    break;
                case 16:
                    for (int i = 0; i < totalSamples; i++)
                        floats[i] = BitConverter.ToInt16(data, dataChunkPos + i * 2) / 32768f;
                    break;
                case 24:
                    for (int i = 0; i < totalSamples; i++)
                    {
                        int o = dataChunkPos + i * 3;
                        int s = data[o] | (data[o + 1] << 8) | (data[o + 2] << 16);
                        if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000); // sign-extend
                        floats[i] = s / 8388608f;
                    }
                    break;
                case 32:
                    for (int i = 0; i < totalSamples; i++)
                        floats[i] = BitConverter.ToInt32(data, dataChunkPos + i * 4) / 2147483648f;
                    break;
            }

        var clip = AudioClip.Create(clipName, samplesPerChannel, numChannels, sampleRate, false);
        clip.SetData(new Il2CppStructArray<float>(floats), 0);
        clip.hideFlags |= HideFlags.HideAndDontSave;
        return clip;
    }
}
