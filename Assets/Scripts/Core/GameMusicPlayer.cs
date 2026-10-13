using UnityEngine;

/// <summary>
/// Persistent looping music source. Put the licensed game theme at
/// Assets/Resources/Music/gamemusic.mp3 (or another supported audio format).
/// </summary>
public class GameMusicPlayer : MonoBehaviour
{
    private const string MusicResourcePath = "Music/gamemusic";
    private const string VolumePreferenceKey = "Audio.MusicVolume";
    private const float DefaultVolume = 0.22f;
    private static GameMusicPlayer instance;

    private AudioSource musicSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateMusicPlayer()
    {
        if (instance != null)
        {
            return;
        }

        GameObject musicObject = new GameObject("GameMusicPlayer");
        instance = musicObject.AddComponent<GameMusicPlayer>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = GetMusicVolume();

        AudioClip theme = Resources.Load<AudioClip>(MusicResourcePath);
        if (theme != null)
        {
            musicSource.clip = theme;
            musicSource.Play();
        }
    }

    public static void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumePreferenceKey, volume);

        if (instance != null && instance.musicSource != null)
        {
            instance.musicSource.volume = volume;
        }
    }

    public static float GetMusicVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreferenceKey, DefaultVolume));
    }
}
