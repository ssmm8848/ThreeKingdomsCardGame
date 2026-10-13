from __future__ import annotations

import hashlib
import os
import re
import sys
import uuid
import wave
from pathlib import Path

os.environ.setdefault("COSYVOICE_DISABLE_WETEXT", "1")

PROJECT_ROOT = Path(__file__).resolve().parents[1]
COSYVOICE_ROOT = Path(os.environ.get("COSYVOICE_ROOT", r"D:\Unity\CosyVoice"))
MODEL_DIR = COSYVOICE_ROOT / "pretrained_models" / "Fun-CosyVoice3-0.5B"
ASSET_ROOT = PROJECT_ROOT / "Assets"
VOICE_ROOT = ASSET_ROOT / "Resources" / "Audio" / "Voices"
PROFILE_ROOT = PROJECT_ROOT / "Tools" / "GeneratedCharacterVoiceProfiles"
AUDIO_META_TEMPLATE = ASSET_ROOT / "Resources" / "Audio" / "SFX" / "attack_slash.ogg.meta"

BASE_PROMPTS = {
    "female": COSYVOICE_ROOT / "asset" / "zero_shot_prompt.wav",
    "male": COSYVOICE_ROOT / "asset" / "cross_lingual_prompt.wav",
}
FEMALE_TRANSCRIPT = "希望你以后能够做的比我还好呦。"
INSTRUCT_PREFIX = "You are a helpful assistant. "
INSTRUCT_SUFFIX = "<|endofprompt|>"
ZERO_SHOT_PREFIX = "You are a helpful assistant.<|endofprompt|>"

CARDS = {
    "sha": "杀！这一招，直取你的要害！",
    "shan": "闪开！你的招式还伤不到我！",
    "tao": "稳住心神，伤势会慢慢好转。",
}

CHARACTERS = {
    "BaiYuan": {
        "gender": "female",
        "style": "A young adult Mandarin-speaking woman with a clear, bright, light voice and gentle warmth. She sounds agile and protective, poised like a graceful martial heroine. Natural delivery, not childish, not breathy, no shouting, no singing.",
        "profile": "清风掠过长空，我会守在你身旁，替你挡下迎面而来的刀锋。",
        "skill": "轻羽随风，替你挡下这一击！",
    },
    "ChiXiao": {
        "gender": "male",
        "style": "A Mandarin-speaking male warrior with a low-mid chest voice, a little husky and rough-edged, forceful and resolute. He speaks in clipped, confident phrases with controlled intensity, never comical, no shouting, no singing.",
        "profile": "刀锋已出，便没有回头路。此战由我开路，谁也别想阻拦。",
        "skill": "烈锋出鞘，趁势再攻！",
    },
    "QingLan": {
        "gender": "female",
        "style": "A Mandarin-speaking woman with a cool, clear, soft-resonant mid voice. Calm, graceful and perceptive, with quiet confidence like a composed strategist. Crisp diction, not childish, not whispering, no singing.",
        "profile": "潮声未歇，局势仍有转圜。稳住心神，再看下一步。",
        "skill": "回澜既起，手牌再添一张！",
    },
    "XuanHeng": {
        "gender": "male",
        "style": "A mature Mandarin-speaking man with a deep, warm, resonant voice. Measured and authoritative, calm like a seasoned strategist who has already seen the whole board. Restrained, deliberate diction, no shouting, no singing.",
        "profile": "棋局已定，不必急于一时。按计行事，胜负自见分晓。",
        "skill": "震策已定，弃牌，或承受代价！",
    },
    "YanZhiWei": {
        "gender": "female",
        "style": "A Mandarin-speaking woman with a smooth lower-mid register, composed and observant, with a subtle mysterious edge like a discerning diviner. Precise and calm rather than breathy or theatrical, no whispering, no singing.",
        "profile": "灯影之下，细节从不说谎。你所隐瞒的，已经写在眼中。",
        "skill": "照见此谶，你的破绽无所遁形！",
    },
    "GuHanchuan": {
        "gender": "male",
        "style": "A Mandarin-speaking man with a grounded, sturdy mid-low voice and warm resonance. Practical and dependable, like a skilled craftsman who speaks plainly and knows his tools. Natural, confident delivery, no shouting, no singing.",
        "profile": "这件器物刚好合用，装上它，战局还能多一分把握。",
        "skill": "巧手成器，装备之后再得一张！",
    },
    "LiuQingwu": {
        "gender": "female",
        "style": "A Mandarin-speaking woman with a nimble, bright mid-high voice and light, quick articulation. Warm and self-assured, with the agile spirit of a wind-moving fighter. Clear diction, not childish, no shouting, no singing.",
        "profile": "风从身后起，借它转身，刚好躲过这一招。",
        "skill": "借风回身，这一击就此落空！",
    },
    "ShenZhaoye": {
        "gender": "male",
        "style": "A young adult Mandarin-speaking man with a controlled mid-low voice, dark smooth resonance and a faint smoky edge. He sounds secretive, calculating and quietly intense, never melodramatic, no shouting, no singing.",
        "profile": "以一纸契约换两分先机，这笔代价，我付得起。",
        "skill": "燃我一契，换取两张先机！",
    },
}

CHARACTER_ASSET_DIRS = (
    ASSET_ROOT / "ScriptableObjects" / "Characters",
    ASSET_ROOT / "Resources" / "Characters",
)
CARD_ASSET_DIR = ASSET_ROOT / "Scripts" / "Cards"


def stable_seed(key: str) -> int:
    return int.from_bytes(hashlib.sha256(key.encode("utf-8")).digest()[:4], "little") % 100000000 + 1


def collect_audio(model, text: str, **kwargs):
    import torch

    chunks = []
    with torch.inference_mode():
        for item in model.inference_instruct2(text, **kwargs):
            chunks.append(item["tts_speech"].detach().cpu())
    if not chunks:
        raise RuntimeError(f"CosyVoice returned no audio for: {text}")
    return torch.cat(chunks, dim=-1)


def collect_zero_shot_audio(model, text: str, prompt_text: str, prompt_wav: Path):
    import torch

    chunks = []
    with torch.inference_mode():
        for item in model.inference_zero_shot(text, prompt_text, str(prompt_wav), stream=False, speed=1.0):
            chunks.append(item["tts_speech"].detach().cpu())
    if not chunks:
        raise RuntimeError(f"CosyVoice returned no audio for: {text}")
    return torch.cat(chunks, dim=-1)


def save_audio(audio, sample_rate: int, path: Path) -> None:
    import torchaudio

    path.parent.mkdir(parents=True, exist_ok=True)
    torchaudio.save(str(path), audio, sample_rate, encoding="PCM_S", bits_per_sample=16)
    with wave.open(str(path), "rb") as wav:
        duration = wav.getnframes() / float(wav.getframerate())
    if duration < 0.35 or path.stat().st_size < 2048:
        raise RuntimeError(f"Generated clip is invalid ({duration:.2f}s): {path}")


def ensure_audio_meta(audio_path: Path) -> str:
    meta_path = audio_path.with_suffix(audio_path.suffix + ".meta")
    content = meta_path.read_text(encoding="utf-8") if meta_path.exists() else ""
    match = re.search(r"^guid: ([0-9a-f]{32})$", content, re.MULTILINE)
    if match:
        return match.group(1)

    template = AUDIO_META_TEMPLATE.read_text(encoding="utf-8-sig")
    match = re.search(r"^guid: ([0-9a-f]{32})$", template, re.MULTILINE)
    if not match:
        raise RuntimeError(f"Bad Unity audio meta template: {AUDIO_META_TEMPLATE}")
    guid = uuid.uuid4().hex
    content = template.replace(f"guid: {match.group(1)}", f"guid: {guid}", 1)
    content = content.replace("forceToMono: 0", "forceToMono: 1", 1)
    meta_path.write_text(content, encoding="utf-8", newline="")
    return guid


def ensure_folder_meta(folder: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    meta_path = folder.with_name(folder.name + ".meta")
    if meta_path.exists():
        return
    content = (
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )
    meta_path.write_text(content, encoding="utf-8", newline="")


def asset_guid(path: Path) -> str:
    meta_path = path.with_suffix(path.suffix + ".meta")
    content = meta_path.read_text(encoding="utf-8-sig")
    match = re.search(r"^guid: ([0-9a-f]{32})$", content, re.MULTILINE)
    if not match:
        raise RuntimeError(f"No Unity GUID in {meta_path}")
    return match.group(1)


def find_character_asset(stem: str) -> Path:
    for folder in CHARACTER_ASSET_DIRS:
        path = folder / f"{stem}.asset"
        if path.is_file():
            return path
    raise FileNotFoundError(f"No CharacterData asset found for {stem}")


def replace_card_voice_entries(character_asset: Path, entries: list[tuple[str, str]]) -> None:
    card_records = []
    for card_stem, clip_guid in entries:
        card_path = CARD_ASSET_DIR / f"{card_stem}.asset"
        card_records.append(
            f"  - card: {{fileID: 11400000, guid: {asset_guid(card_path)}, type: 2}}\n"
            f"    voiceClip: {{fileID: 8300000, guid: {clip_guid}, type: 3}}"
        )
    block = "  cardVoices:\n" + "\n".join(card_records)
    content = character_asset.read_text(encoding="utf-8-sig")
    if re.search(r"(?m)^  cardVoices:", content):
        content = re.sub(r"(?ms)^  cardVoices:.*?(?=^  [A-Za-z_][A-Za-z0-9_]*:|\Z)", block + "\n", content, count=1)
    else:
        content = content.rstrip() + "\n" + block + "\n"
    character_asset.write_text(content, encoding="utf-8", newline="")


def update_skill_voice(character_asset: Path, clip_guid: str) -> None:
    content = character_asset.read_text(encoding="utf-8-sig")
    line = f"  skillVoice: {{fileID: 8300000, guid: {clip_guid}, type: 3}}"
    if re.search(r"(?m)^  skillVoice:.*$", content):
        content = re.sub(r"(?m)^  skillVoice:.*$", line, content, count=1)
    else:
        content = content.rstrip() + "\n" + line + "\n"
    character_asset.write_text(content, encoding="utf-8", newline="")


def main() -> None:
    for path in (*BASE_PROMPTS.values(), MODEL_DIR / "cosyvoice3.yaml", AUDIO_META_TEMPLATE):
        if not path.is_file():
            raise FileNotFoundError(path)

    # This helper loads one local CosyVoice model directly so instruct2 is
    # available. Stop the existing Gradio service before running it to free VRAM.
    sys.path.insert(0, str(COSYVOICE_ROOT))
    sys.path.insert(0, str(COSYVOICE_ROOT / "third_party" / "Matcha-TTS"))
    from cosyvoice.cli.cosyvoice import AutoModel
    from cosyvoice.utils.common import set_all_random_seed

    ensure_folder_meta(VOICE_ROOT / "CharacterCards")
    PROFILE_ROOT.mkdir(parents=True, exist_ok=True)
    print("Loading local CosyVoice3 model...", flush=True)
    model = AutoModel(model_dir=str(MODEL_DIR))

    for index, (stem, persona) in enumerate(CHARACTERS.items(), 1):
        character_asset = find_character_asset(stem)
        base_prompt = BASE_PROMPTS[persona["gender"]]
        profile_audio = PROFILE_ROOT / f"{stem}.wav"
        profile_text = persona["profile"]
        instructions = INSTRUCT_PREFIX + persona["style"] + INSTRUCT_SUFFIX

        print(f"[{index}/{len(CHARACTERS)}] Building voice identity: {stem}", flush=True)
        set_all_random_seed(stable_seed(f"persona:{stem}"))
        profile_speech = collect_audio(
            model,
            profile_text,
            instruct_text=instructions,
            prompt_wav=str(base_prompt),
            stream=False,
            speed=1.0,
        )
        save_audio(profile_speech, model.sample_rate, profile_audio)
        (PROFILE_ROOT / f"{stem}.txt").write_text(profile_text, encoding="utf-8")

        voice_entries = []
        jobs = [*CARDS.items(), ("skill", persona["skill"])]
        for card_stem, line in jobs:
            if card_stem == "skill":
                clip_path = VOICE_ROOT / "Characters" / f"{stem}.wav"
            else:
                clip_path = VOICE_ROOT / "CharacterCards" / f"{stem}_{card_stem}.wav"

            set_all_random_seed(stable_seed(f"{stem}:{card_stem}"))
            voice = collect_zero_shot_audio(
                model,
                line,
                ZERO_SHOT_PREFIX + profile_text,
                profile_audio,
            )
            save_audio(voice, model.sample_rate, clip_path)
            clip_guid = ensure_audio_meta(clip_path)
            if card_stem == "skill":
                update_skill_voice(character_asset, clip_guid)
            else:
                voice_entries.append((card_stem, clip_guid))
            print(f"  OK {card_stem}: {clip_path.relative_to(PROJECT_ROOT)}", flush=True)

        replace_card_voice_entries(character_asset, voice_entries)

    print(
        f"Finished {len(CHARACTERS) * (len(CARDS) + 1)} character-specific game voices "
        f"and {len(CHARACTERS)} reusable voice profiles.",
        flush=True,
    )


if __name__ == "__main__":
    main()
