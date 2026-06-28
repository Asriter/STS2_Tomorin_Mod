using BaseLib.Patches.Hooks;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;

namespace STS2_Tomorin_Mod.Audio;

public static class CustomAudioController
{
    private static AudioStreamPlayer? _customBgmPlayer;
    private static AudioStreamPlayer? _customSfxPlayer;
    
    private static readonly Dictionary<string, AudioStream> _sfxDict = new Dictionary<string, AudioStream>();

    public static AudioStreamPlayer CustomBgmPlayer
    {
        get
        {
            if (_customBgmPlayer == null || !GodotObject.IsInstanceValid(_customBgmPlayer))
            {
                _customBgmPlayer = new AudioStreamPlayer();
                // 添加到全局的场景树中，确保跨场景不会被销毁
                var tree = Engine.GetMainLoop() as SceneTree;
                tree?.Root.AddChild(_customBgmPlayer);
            
                // 可选：设置音频总线，可以直接连到 Godot 的主音量
                _customBgmPlayer.Bus = "Master";
            }
            
            return _customBgmPlayer;
        }
    }

    public static AudioStreamPlayer CustomSfxPlayer
    {
        get
        {
            if (_customSfxPlayer == null || !GodotObject.IsInstanceValid(_customSfxPlayer))
            {
                _customSfxPlayer = new AudioStreamPlayer();
                // 添加到全局的场景树中，确保跨场景不会被销毁
                var tree = Engine.GetMainLoop() as SceneTree;
                tree?.Root.AddChild(_customSfxPlayer);
            
                // 可选：设置音频总线，可以直接连到 Godot 的主音量
                _customSfxPlayer.Bus = "Master";
            }
            
            return _customSfxPlayer;
        }
    }

    public static void PlayMusic(string path)
    {
        //调整音量大小
        CustomBgmPlayer.VolumeDb = SaveManager.Instance.SettingsSave.VolumeBgm;
        AudioStream bgmStream = GD.Load<AudioStream>(GetBgmPath(path));
        if (bgmStream == null)
        {
            StopMusic();
            return;
        }
        
        CustomBgmPlayer.Stream = bgmStream;
        CustomBgmPlayer.Play();
    }

    public static void StopMusic()
    {
        CustomBgmPlayer.Stop();
    }
    
    //音效相关
    public static void PlaySfx(string path)
    {
        var player = CustomSfxPlayer;
        player.VolumeDb = SaveManager.Instance.SettingsSave.VolumeSfx;

        if (!_sfxDict.TryGetValue(path, out var sfxStream))
        {
            sfxStream = GD.Load<AudioStream>(GetSfxPath(path));
            if (sfxStream == null)
                return;

            _sfxDict[path] = sfxStream;
        }
        
        player.Stream = sfxStream;

        // 6. 开始播放
        player.Play();
    }

    private static string GetSfxPath(string path)
    {
        return $"res://STS2_Tomorin_Mod/sound/sfx/{path}.mp3";
    }

    private static string GetBgmPath(string path)
    {
        return $"res://STS2_Tomorin_Mod/sound/music/{path}.mp3";
    }
}
