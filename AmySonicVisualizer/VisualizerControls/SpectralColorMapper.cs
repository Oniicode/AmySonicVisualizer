using SharpDX.Mathematics.Interop;

namespace AmySonicVisualizer.VisualizerControls
{
	public static class SpectralColorMapper
	{
		/// <summary>
		/// Gets the packed B8G8R8A8 integer color representation for a given intensity.
		/// Useful for fast memory copying to hardware bitmaps (e.g., Spectrogram).
		/// </summary>
		public static int GetSpectralColorInt(float intensity)
		{
			if (intensity <= 0.0f) 
				return (255 << 24) | (12 << 16) | (10 << 8) | 18;

			int r, g, b;
			if (intensity < 0.25f)
			{
				float t = intensity / 0.25f;
				r = (int)(45 * t); g = 0; b = (int)(90 * t);
			}
			else if (intensity < 0.55f)
			{
				float t = (intensity - 0.25f) / 0.30f;
				r = (int)(45 + (180 - 45) * t); g = (int)(15 * t); b = (int)(90 + (130 - 90) * t);
			}
			else if (intensity < 0.85f)
			{
				float t = (intensity - 0.55f) / 0.30f;
				r = (int)(180 + (40 - 180) * t); g = (int)(15 + (210 - 15) * t); b = (int)(220 + (255 - 220) * t);
			}
			else
			{
				float t = (intensity - 0.85f) / 0.15f;
				r = (int)(40 + (255 - 40) * t); g = (int)(210 + (255 - 210) * t); b = 255;
			}

			// B8G8R8A8_UNorm memory packing translates seamlessly with typical integer packing 
			// since little endian stores the least significant byte first.
			return (255 << 24) | (r << 16) | (g << 8) | b;
		}

		/// <summary>
		/// Gets the RawColor4 representation for a given intensity.
		/// Useful for Direct2D SolidColorBrushes (e.g., EQ Spectrum Bins).
		/// </summary>
		public static RawColor4 GetSpectralColorRaw4(float intensity, float alpha = 1.0f)
		{
			if (intensity <= 0.0f) 
				return new RawColor4(12f / 255f, 10f / 255f, 18f / 255f, alpha);

			float r, g, b;
			if (intensity < 0.25f)
			{
				float t = intensity / 0.25f;
				r = 45f * t; g = 0f; b = 90f * t;
			}
			else if (intensity < 0.55f)
			{
				float t = (intensity - 0.25f) / 0.30f;
				r = 45f + (180f - 45f) * t; g = 15f * t; b = 90f + (130f - 90f) * t;
			}
			else if (intensity < 0.85f)
			{
				float t = (intensity - 0.55f) / 0.30f;
				r = 180f + (40f - 180f) * t; g = 15f + (210f - 15f) * t; b = 220f + (255f - 220f) * t;
			}
			else
			{
				float t = (intensity - 0.85f) / 0.15f;
				r = 40f + (255f - 40f) * t; g = 210f + (255f - 210f) * t; b = 255f;
			}

			return new RawColor4(r / 255f, g / 255f, b / 255f, alpha);
		}
	}
}