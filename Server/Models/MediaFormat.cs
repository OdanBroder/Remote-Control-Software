using System;
using SIPSorceryMedia.Abstractions;
using SIPSorcery.Net;

namespace Server.Models
{
    public class MediaFormat
    {
        public SDPMediaTypesEnum MediaType { get; private set; }
        public int PayloadType { get; private set; }
        public string Codec { get; private set; }
        public int ClockRate { get; private set; }

        public MediaFormat(SDPMediaTypesEnum mediaType, int payloadType, string codec, int clockRate)
        {
            if (string.IsNullOrEmpty(codec))
                throw new ArgumentException("Codec cannot be null or empty.", nameof(codec));

            if (clockRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(clockRate), "Clock rate must be greater than zero.");

            MediaType = mediaType;
            PayloadType = payloadType;
            Codec = codec;
            ClockRate = clockRate;
        }

        public override string ToString()
        {
            return $"{PayloadType} {Codec} {ClockRate}";
        }

        public static MediaFormat FromString(string mediaFormatString)
        {
            var parts = mediaFormatString.Split(' ');

            if (parts.Length != 3)
                throw new ArgumentException("Invalid media format string.", nameof(mediaFormatString));

            if (!int.TryParse(parts[0], out int payloadType))
                throw new ArgumentException("Invalid payload type.", nameof(mediaFormatString));

            string codec = parts[1];
            if (!int.TryParse(parts[2], out int clockRate))
                throw new ArgumentException("Invalid clock rate.", nameof(mediaFormatString));

            return new MediaFormat(SDPMediaTypesEnum.video, payloadType, codec, clockRate);
        }
    }
}
