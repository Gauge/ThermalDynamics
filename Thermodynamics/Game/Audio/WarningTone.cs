using ThermalDynamics;
using VRage.Utils;
using VRageMath;
using System;
using System.Collections.Generic;

namespace Thermodynamics.Audio
{
    public sealed class PwmAudioGeneratorDefinition
    {
        public double PreSilenceMs { get; set; }
        public double PostSilenceMs { get; set; } = 100;

        public int Repeat { get; set; } = 1;

        public List<IToneStep> Steps { get; } = new List<IToneStep>();
        
        
        // todo: make this a definition later on
        public static readonly Dictionary<string, PwmAudioGeneratorDefinition> Default =
            new Dictionary<string, PwmAudioGeneratorDefinition>()
            {
                ["overheat-warning"] = new PwmAudioGeneratorDefinition
                {
                    Repeat = 8,
                    Steps =
                    {
                        new ToneStep
                        {
                            FrequencyHz = 592,
                            DurationMs = 40,
                            DutyCycle = 0.25
                        },
                        new ToneStep
                        {
                            FrequencyHz = 837,
                            DurationMs = 40,
                            DutyCycle = 0.25
                        },
                    }
                },

                ["overheat-critical"] = new PwmAudioGeneratorDefinition
                {
                    PostSilenceMs = 0,
                    Repeat = 1,
                    Steps =
                    {
                        new ToneStep
                        {
                            FrequencyHz = 514,
                            DurationMs = 205,
                            DutyCycle = 0.25
                        },

                        new Silence
                        {
                            DurationMs = 168
                        },

                        new ToneStep
                        {
                            FrequencyHz = 514,
                            DurationMs = 410,
                            DutyCycle = 0.25
                        },

                        new Silence
                        {
                            DurationMs = 168
                        }
                    }
                }
            };
        
    }
    
    public interface IToneStep
    {
        double FrequencyHz { get; }
        double DurationMs { get; }
        double DutyCycle { get; }
        double Gain { get; }
    }
        
    public sealed class ToneStep : IToneStep
    {
        public double FrequencyHz { get; set; }
        public double DurationMs { get; set; }
        public double DutyCycle { get; set; } = 0.25; // 0.0 .. 1.0
        public double Gain { get; set; } = 0.8; // 0.0 .. 1.0
    }
        
    public sealed class Silence : IToneStep
    {
        public double FrequencyHz => 0;
        public double DurationMs { get; set; }
        public double DutyCycle => .5;
        public double Gain => .5;
    }
    
    public static class WarningToneGenerator
    {
        public const int SAMPLE_RATE = 24000;

        public static byte[] GeneratePcm16(PwmAudioGeneratorDefinition pwmAudioGeneratorDefinition)
        {
            var output = new List<byte>();

            AddSilence(output, pwmAudioGeneratorDefinition.PreSilenceMs);

            double phase = 0.0;

            if (pwmAudioGeneratorDefinition.Repeat <= 0 || pwmAudioGeneratorDefinition.Steps.Count == 0)
            {
                LogHelper.Log(
                    MyLogSeverity.Warning,
                    "WarningToneGenerator: Warning tone generated with silent audio.");
            }
            else
            {
                for (int i = 0; i < pwmAudioGeneratorDefinition.Repeat; i++)
                {
                    foreach (var step in pwmAudioGeneratorDefinition.Steps)
                    {
                        AddPwmTone(output, step, ref phase);
                    }
                }
            }

            AddSilence(output, pwmAudioGeneratorDefinition.PostSilenceMs);

            return output.ToArray();
        }


        private static void AddSilence(
            List<byte> output,
            double durationMs,
            int sampleRate = SAMPLE_RATE)
        {
            int sampleCount = MsToSamples(durationMs, sampleRate);

            // 16-bit PCM = 2 bytes per sample.
            int byteCount = sampleCount * 2;

            for (int i = 0; i < byteCount; i++)
            {
                output.Add(0);
            }
        }


        private static void AddPwmTone(
            List<byte> output,
            IToneStep step,
            ref double phase,
            int sampleRate = SAMPLE_RATE)
        {
            
            double duty = MathHelper.Clamp(step.DutyCycle, 0.001, 0.999);
            double gain = MathHelper.Clamp(step.Gain, 0.001, 1.0);
            double frequency = Math.Max(step.FrequencyHz, 0.001);
            
            int sampleCount =
                MsToSamples(step.DurationMs, sampleRate);

            double phaseIncrement = frequency / sampleRate;

            short amplitude =
                (short)(short.MaxValue * gain);

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = phase < duty
                    ? amplitude
                    : (short)-amplitude;

                AddSample16(output, sample);

                phase += phaseIncrement;

                if (phase >= 1.0)
                {
                    phase -= Math.Floor(phase);
                }
            }
        }

        private static void AddSample16(
            List<byte> output,
            short sample)
        {
            output.Add((byte)(sample & 0xFF));
            output.Add((byte)((sample >> 8) & 0xFF));
        }


        private static int MsToSamples(
            double ms,
            int sampleRate)
        {
            return (int)Math.Round(
                sampleRate * (ms / 1000.0));
        }
    }
}