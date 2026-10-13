using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Loads game sounds and queues card and character skill voice lines.</summary>
public class GameSfxPlayer : MonoBehaviour
{
    private const string ResourceRoot = "Audio/SFX/";
    private const string VolumePreferenceKey = "Audio.SfxVolume";
    private const float DefaultVolume = 0.85f;
    private static GameSfxPlayer instance;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly HashSet<Button> registeredButtons = new HashSet<Button>();
    private AudioSource source;
    private AudioSource voiceSource;
    private Coroutine voicePlaybackCoroutine;
    private float lastCardDrawTime = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreatePlayer()
    {
        if (instance != null)
        {
            return;
        }

        GameObject sfxObject = new GameObject("GameSfxPlayer");
        instance = sfxObject.AddComponent<GameSfxPlayer>();
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

        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = GetSfxVolume();

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = GetSfxVolume() * 0.9f;

        SceneManager.sceneLoaded += OnSceneLoaded;
        RegisterSceneButtons();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        registeredButtons.RemoveWhere(button => button == null);
        RegisterSceneButtons();
    }

    private void RegisterSceneButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            RegisterButton(buttons[i]);
        }
    }

    public static void RegisterButton(Button button)
    {
        if (instance == null || button == null || instance.registeredButtons.Contains(button))
        {
            return;
        }

        // These controls have their own interaction sounds or are direct target panels.
        if (button.GetComponent<CardButton>() != null
            || button.GetComponent<TargetButton>() != null
            || button.GetComponent<BattleCharacterPanel>() != null)
        {
            return;
        }

        instance.registeredButtons.Add(button);
        button.onClick.AddListener(PlayButtonClick);
    }

    public static void PlayButtonClick()
    {
        Play("ui_click", 0.24f);
    }

    public static void PlayCardSelect()
    {
        Play("card_select", 0.24f);
    }

    public static void PlayTargetSelect()
    {
        Play("ui_target", 0.2f);
    }

    public static void PlayCardAction(CardData card)
    {
        if (card == null)
        {
            return;
        }

        switch (card.effectType)
        {
            case CardEffectType.Attack:
                Play("attack_slash", 0.46f);
                return;
            case CardEffectType.Defense:
                PlayDefenseBlock();
                return;
            case CardEffectType.Heal:
                Play("heal_restore", 0.32f);
                return;
            case CardEffectType.Weapon:
            case CardEffectType.Armor:
            case CardEffectType.Treasure:
            case CardEffectType.MountPlus:
            case CardEffectType.MountMinus:
                Play("equip_metal", 0.34f);
                return;
        }

        // Use the generic card sound only for cards without a dedicated action sound.
        Play("card_play", 0.2f);
    }

    public static void PlayCardVoice(CardData card)
    {
        if (card != null)
        {
            PlayVoiceImmediately(card.voiceClip);
        }
    }

    public static bool PlayCardVoice(Player owner, CardData card)
    {
        if (card == null)
        {
            return false;
        }

        AudioClip clip = owner != null && owner.characterData != null
            ? owner.characterData.GetCardVoice(card)
            : null;
        if (clip == null)
        {
            clip = card.voiceClip;
        }

        if (clip == null)
        {
            return false;
        }

        PlayVoiceImmediately(clip);
        return true;
    }

    public static void PlaySkillVoice(Player owner)
    {
        if (owner != null && owner.characterData != null)
        {
            PlayVoiceImmediately(owner.characterData.skillVoice);
        }
    }

    public static void PlayCharacterCardVoice(Player owner)
    {
        if (owner != null && owner.characterData != null)
        {
            PlayVoiceImmediately(owner.characterData.cardUseVoice);
        }
    }

    private static void PlayVoiceImmediately(AudioClip clip)
    {
        if (instance == null || instance.voiceSource == null || clip == null)
        {
            return;
        }

        if (instance.voicePlaybackCoroutine != null)
        {
            instance.StopCoroutine(instance.voicePlaybackCoroutine);
            instance.voicePlaybackCoroutine = null;
        }

        instance.voiceSource.Stop();
        instance.voiceSource.clip = clip;
        instance.voiceSource.volume = GetSfxVolume() * 0.9f;
        instance.voiceSource.Play();
        instance.voicePlaybackCoroutine = instance.StartCoroutine(
            instance.ClearVoiceAfterPlayback(clip.length + 0.04f)
        );
    }

    private IEnumerator ClearVoiceAfterPlayback(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);

        voiceSource.Stop();
        voiceSource.clip = null;
        voicePlaybackCoroutine = null;
    }

    public static void PlayDamageImpact()
    {
        Play("attack_hit", 0.32f);
    }

    public static void PlayDefenseBlock()
    {
        Play("defense_block", 0.28f);
    }

    public static void PlayHeal()
    {
        Play("heal_restore", 0.32f);
    }

    public static void PlayCardDraw()
    {
        if (instance == null || Time.unscaledTime - instance.lastCardDrawTime < 0.12f)
        {
            return;
        }

        instance.lastCardDrawTime = Time.unscaledTime;
        Play("card_draw", 0.18f);
    }

    public static void PlayCardDiscard()
    {
        Play("card_discard", 0.18f);
    }

    public static void PlayTurnNotice()
    {
        Play("turn_bell", 0.16f);
    }

    public static void SetSfxVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumePreferenceKey, volume);

        if (instance != null && instance.source != null)
        {
            instance.source.volume = volume;
            if (instance.voiceSource != null)
            {
                instance.voiceSource.volume = volume * 0.9f;
            }
        }
    }

    public static float GetSfxVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreferenceKey, DefaultVolume));
    }

    private static void Play(string clipName, float volume)
    {
        if (instance == null || instance.source == null)
        {
            return;
        }

        if (!instance.clips.TryGetValue(clipName, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(ResourceRoot + clipName);
            instance.clips[clipName] = clip;
        }

        if (clip != null)
        {
            instance.source.PlayOneShot(clip, volume);
        }
    }
}
