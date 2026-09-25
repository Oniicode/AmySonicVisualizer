namespace AmySonicVisualizer;

public static class AudioMath
{
    public static string GetNoteName(double frequency)
    {
        if (frequency <= 0) return string.Empty;

        // Convert frequency to MIDI note number (69 is A4 / 440 Hz)
        int noteNumber = (int)Math.Round(12 * Math.Log2(frequency / 440.0) + 69);
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        int octave = (noteNumber / 12) - 1;
        int noteIndex = noteNumber % 12;

        // Handle potential negative indices for extremely low frequencies
        if (noteIndex < 0)
        {
            noteIndex += 12;
            octave--;
        }

        return $"{noteNames[noteIndex]}{octave}";
    }
}