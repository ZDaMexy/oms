// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class AudioSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.AudioSettings";

        /// <summary>
        /// "Audio"
        /// </summary>
        public static LocalisableString AudioSectionHeader => new TranslatableString(getKey(@"audio_section_header"), @"Audio");

        /// <summary>
        /// "Devices"
        /// </summary>
        public static LocalisableString AudioDevicesHeader => new TranslatableString(getKey(@"audio_devices_header"), @"Devices");

        /// <summary>
        /// "Volume"
        /// </summary>
        public static LocalisableString VolumeHeader => new TranslatableString(getKey(@"volume_header"), @"Volume");

        /// <summary>
        /// "Output device"
        /// </summary>
        public static LocalisableString OutputDevice => new TranslatableString(getKey(@"output_device"), @"Output device");

        /// <summary>
        /// "Hitsound stereo separation"
        /// </summary>
        public static LocalisableString PositionalLevel => new TranslatableString(getKey(@"positional_hitsound_audio_level"), @"Hitsound stereo separation");

        /// <summary>
        /// "Master"
        /// </summary>
        public static LocalisableString MasterVolume => new TranslatableString(getKey(@"master_volume"), @"Master");

        /// <summary>
        /// "Master (window inactive)"
        /// </summary>
        public static LocalisableString MasterVolumeInactive => new TranslatableString(getKey(@"master_volume_inactive"), @"Master (window inactive)");

        /// <summary>
        /// "Effect"
        /// </summary>
        public static LocalisableString EffectVolume => new TranslatableString(getKey(@"effect_volume"), @"Effect");

        /// <summary>
        /// "Music"
        /// </summary>
        public static LocalisableString MusicVolume => new TranslatableString(getKey(@"music_volume"), @"Music");

        /// <summary>
        /// "Offset Adjustment"
        /// </summary>
        public static LocalisableString OffsetHeader => new TranslatableString(getKey(@"offset_header"), @"Offset Adjustment");

        /// <summary>
        /// "Audio offset"
        /// </summary>
        public static LocalisableString AudioOffset => new TranslatableString(getKey(@"audio_offset"), @"Audio offset");

        /// <summary>
        /// "Play a few beatmaps to receive a suggested offset!"
        /// </summary>
        public static LocalisableString SuggestedOffsetNote => new TranslatableString(getKey(@"suggested_offset_note"), @"Play a few beatmaps to receive a suggested offset!");

        /// <summary>
        /// "Based on the last {0} play(s), your offset is set correctly!"
        /// </summary>
        public static LocalisableString SuggestedOffsetCorrect(int plays) => new TranslatableString(getKey(@"suggested_offset_correct"), @"Based on the last {0} play(s), your offset is set correctly!", plays);

        /// <summary>
        /// "Based on the last {0} play(s), the suggested offset is {1} ms."
        /// </summary>
        public static LocalisableString SuggestedOffsetValueReceived(int plays, LocalisableString value) => new TranslatableString(getKey(@"suggested_offset_value_received"), @"Based on the last {0} play(s), the suggested offset is {1} ms.", plays, value);

        /// <summary>
        /// "Apply suggested offset"
        /// </summary>
        public static LocalisableString ApplySuggestedOffset => new TranslatableString(getKey(@"apply_suggested_offset"), @"Apply suggested offset");

        /// <summary>
        /// "Offset wizard"
        /// </summary>
        public static LocalisableString OffsetWizard => new TranslatableString(getKey(@"offset_wizard"), @"Offset wizard");

        /// <summary>
        /// "Use experimental audio mode"
        /// </summary>
        public static LocalisableString WasapiLabel => new TranslatableString(getKey(@"wasapi_label"), @"Use experimental audio mode");

        /// <summary>
        /// "This will attempt to initialise the audio engine in a lower latency mode."
        /// </summary>
        public static LocalisableString WasapiTooltip => new TranslatableString(getKey(@"wasapi_tooltip"), @"This will attempt to initialise the audio engine in a lower latency mode.");

        /// <summary>
        /// "Due to reduced latency, your audio offset will need to be adjusted when enabling this setting. Generally expect to subtract 20 - 60 ms from your known value."
        /// </summary>
        public static LocalisableString WasapiNotice => new TranslatableString(getKey(@"wasapi_notice"), @"Due to reduced latency, your audio offset will need to be adjusted when enabling this setting. Generally expect to subtract 20 - 60 ms from your known value.");

        /// <summary>
        /// "自动调整偏移"
        /// </summary>
        public static LocalisableString AutomaticOffset => new TranslatableString(getKey(@"automatic_offset"), @"自动调整偏移");

        /// <summary>
        /// "osu!lazer style：根据上一局表现调整谱面偏移；beatoraja style：仅在 BMS 演奏中逐步调整音符显示时机，不改变音乐或判定。两种方式互斥，beatoraja style 不调整 osu!mania。"
        /// </summary>
        public static LocalisableString AutomaticOffsetHint => new TranslatableString(getKey(@"automatic_offset_hint"), @"osu!lazer style：根据上一局表现调整谱面偏移；beatoraja style：仅在 BMS 演奏中逐步调整音符显示时机，不改变音乐或判定。两种方式互斥，beatoraja style 不调整 osu!mania。");

        /// <summary>
        /// "关闭"
        /// </summary>
        public static LocalisableString AutomaticOffsetOff => new TranslatableString(getKey(@"automatic_offset_off"), @"关闭");

        /// <summary>
        /// "osu!lazer style"
        /// </summary>
        public static LocalisableString AutomaticOffsetLazer => new TranslatableString(getKey(@"automatic_offset_lazer"), @"osu!lazer style");

        /// <summary>
        /// "beatoraja style"
        /// </summary>
        public static LocalisableString AutomaticOffsetBeatoraja => new TranslatableString(getKey(@"automatic_offset_beatoraja"), @"beatoraja style");

        /// <summary>
        /// "BMS 显示偏移（ms）"
        /// </summary>
        public static LocalisableString BmsVisualOffset => new TranslatableString(getKey(@"bms_visual_offset"), @"BMS 显示偏移（ms）");

        /// <summary>
        /// "正值使音符更早到达判定线，负值使音符更晚到达。关闭自动调整后保留当前值；输入 0 可恢复。不改变音乐或判定。"
        /// </summary>
        public static LocalisableString BmsVisualOffsetHint => new TranslatableString(getKey(@"bms_visual_offset_hint"), @"正值使音符更早到达判定线，负值使音符更晚到达。关闭自动调整后保留当前值；输入 0 可恢复。不改变音乐或判定。");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
