from __future__ import annotations

import hashlib
import os
import re
import shutil
import time
import uuid
import wave
from pathlib import Path

from gradio_client import Client, handle_file

PROJECT_ROOT = Path(__file__).resolve().parents[1]
COSYVOICE_ROOT = Path(os.environ.get("COSYVOICE_ROOT", r"D:\Unity\CosyVoice"))
REFERENCE_ROOT = COSYVOICE_ROOT / "asset"
FEMALE_PROMPT = REFERENCE_ROOT / "zero_shot_prompt.wav"
MALE_PROMPT = REFERENCE_ROOT / "cross_lingual_prompt.wav"
FEMALE_TRANSCRIPT = "希望你以后能够做的比我还好呦。"
VOICE_ROOT = PROJECT_ROOT / "Assets" / "Resources" / "Audio" / "Voices"
AUDIO_META_TEMPLATE = PROJECT_ROOT / "Assets" / "Resources" / "Audio" / "SFX" / "attack_slash.ogg.meta"
GRADIO_URL = "http://127.0.0.1:8000"

# Female and male timbres use the two sample speakers shipped with the local
# CosyVoice installation. The files in this list are game dialogue, not prompt
# samples, and each clip is tied to one card or character skill.
CARD_LINES = [
    ("sha", "Assets/Scripts/Cards/sha.asset", "杀！这一招，直取你的要害！", "male"),
    ("shan", "Assets/Scripts/Cards/shan.asset", "闪开！你的招式还伤不到我！", "female"),
    ("tao", "Assets/Scripts/Cards/tao.asset", "稳住心神，伤势会慢慢好转。", "female"),
    ("chaiqiao", "Assets/Scripts/Cards/chaiqiao.asset", "桥断了，你的退路也断了！", "male"),
    ("DuanCe", "Assets/Scripts/Cards/DuanCe.asset", "谋定而动，局势尽在掌握。", "male"),
    ("FenYing", "Assets/Scripts/Cards/FenYing.asset", "焚营！让他们无处可退！", "male"),
    ("HanXingNu", "Assets/Scripts/Cards/HanXingNu.asset", "寒星连发，箭箭不落空！", "male"),
    ("JiLing", "Assets/Scripts/Cards/JiLing.asset", "军令如山，诸军即刻听令！", "male"),
    ("QingDeng", "Assets/Scripts/Cards/QingDeng.asset", "青灯长明，这一局还未结束。", "female"),
    ("YunWenJia", "Assets/Scripts/Cards/YunWenJia.asset", "云纹护体，刀兵难近！", "male"),
    ("ChaWei", "Assets/Resources/SupplementalCards/ChaWei.asset", "细察微末，破绽就在眼前！", "female"),
    ("DuanLiang", "Assets/Resources/SupplementalCards/DuanLiang.asset", "粮道已断，敌军撑不了多久！", "male"),
    ("FengHuo", "Assets/Resources/SupplementalCards/FengHuo.asset", "烽火传令，各军即刻出击！", "male"),
    ("HuXin", "Assets/Resources/SupplementalCards/HuXin.asset", "护住心脉，我替你挡下这一击！", "female"),
    ("SouHen", "Assets/Resources/SupplementalCards/SouHen.asset", "循着此痕，真相很快就会现身！", "female"),
    ("TanNang", "Assets/Resources/SupplementalCards/TanNang.asset", "看准时机，把他的牌拿来！", "male"),
    ("TieBi", "Assets/Resources/SupplementalCards/TieBi.asset", "铁壁横身，休想再进一步！", "male"),
    ("WeiJi", "Assets/Resources/SupplementalCards/WeiJi.asset", "真假难辨，正好让他们互相猜疑！", "female"),
    ("XiJi", "Assets/Resources/SupplementalCards/XiJi.asset", "旧迹已除，任谁也查不出来！", "female"),
]

CHARACTER_LINES = [
    ("BaiYuan", "轻羽随风，替你挡下这一击！", "female"),
    ("ChiXiao", "烈锋出鞘，趁势再攻！", "male"),
    ("QingLan", "回澜既起，手牌再添一张！", "female"),
    ("XuanHeng", "震策已定，弃牌，或承受代价！", "male"),
    ("YanZhiWei", "照见此谶，你的破绽无所遁形！", "female"),
    ("GuHanchuan", "巧手成器，装备之后再得一张！", "male"),
    ("LiuQingwu", "借风回身，这一击就此落空！", "female"),
    ("ShenZhaoye", "燃我一契，换取两张先机！", "male"),
]


def stable_seed(key: str) -> int:
    return int.from_bytes(hashlib.sha256(key.encode("utf-8")).digest()[:4], "little") % 100000000 + 1


def make_audio(client: Client, key: str, text: str, voice: str, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    if voice == "female":
        mode = "3s极速复刻"
        tts_text = text
        prompt_text = FEMALE_TRANSCRIPT
        prompt_audio = handle_file(FEMALE_PROMPT)
    else:
        # CosyVoice3 requires the task prefix for cross-lingual inference.
        mode = "跨语种复刻"
        tts_text = "You are a helpful assistant.<|endofprompt|>" + text
        prompt_text = ""
        prompt_audio = handle_file(MALE_PROMPT)

    last_error = None
    for attempt in range(1, 3):
        try:
            result = client.predict(
                tts_text,
                mode,
                "",
                prompt_text,
                prompt_audio,
                None,
                "",
                stable_seed(key),
                False,
                0.96,
                api_name="/generate_audio",
            )
            if isinstance(result, tuple):
                raise TypeError(f"Unexpected audio response tuple: {type(result[0])}")
            generated = Path(str(result))
            if not generated.is_file() or generated.stat().st_size < 2048:
                raise RuntimeError(f"CosyVoice returned an invalid audio file: {result!r}")
            shutil.copy2(generated, target)
            with wave.open(str(target), "rb") as wav:
                duration = wav.getnframes() / float(wav.getframerate())
            if duration < 0.25:
                raise RuntimeError(f"Generated clip is too short ({duration:.2f}s): {target}")
            print(f"OK {key}: {duration:.2f}s, {target.relative_to(PROJECT_ROOT)}", flush=True)
            return
        except Exception as exc:
            last_error = exc
            print(f"Attempt {attempt}/2 failed for {key}: {exc}", flush=True)
            time.sleep(1.5)
    raise RuntimeError(f"CosyVoice failed for {key}") from last_error


def ensure_unity_reference(asset_path: Path, field_name: str, audio_path: Path) -> None:
    meta_path = audio_path.with_suffix(audio_path.suffix + ".meta")
    content = meta_path.read_text(encoding="utf-8") if meta_path.exists() else ""
    guid_match = re.search(r"^guid: ([0-9a-f]{32})$", content, re.MULTILINE)
    if not guid_match:
        template = AUDIO_META_TEMPLATE.read_text(encoding="utf-8-sig")
        template_guid = re.search(r"^guid: ([0-9a-f]{32})$", template, re.MULTILINE)
        if not template_guid:
            raise RuntimeError(f"Bad Unity audio meta template: {AUDIO_META_TEMPLATE}")
        guid = uuid.uuid4().hex
        content = template.replace(
            f"guid: {template_guid.group(1)}",
            f"guid: {guid}",
            1,
        )
        content = content.replace("forceToMono: 0", "forceToMono: 1", 1)
        meta_path.write_text(content, encoding="utf-8", newline="")
    else:
        guid = guid_match.group(1)

    asset_text = asset_path.read_text(encoding="utf-8-sig")
    ref_line = f"  {field_name}: {{fileID: 8300000, guid: {guid}, type: 3}}"
    pattern = rf"(?m)^  {re.escape(field_name)}:.*$"
    if re.search(pattern, asset_text):
        asset_text = re.sub(pattern, ref_line, asset_text, count=1)
    else:
        asset_text = asset_text.rstrip() + "\n" + ref_line + "\n"
    asset_path.write_text(asset_text, encoding="utf-8", newline="")


def main() -> None:
    for path in (FEMALE_PROMPT, MALE_PROMPT, AUDIO_META_TEMPLATE):
        if not path.is_file():
            raise FileNotFoundError(path)

    client = Client(GRADIO_URL, verbose=False)
    jobs = []
    for stem, asset_relative, line, voice in CARD_LINES:
        audio = VOICE_ROOT / "Cards" / f"{stem}.wav"
        jobs.append((f"card:{stem}", line, voice, audio, PROJECT_ROOT / asset_relative, "voiceClip"))
    for stem, line, voice in CHARACTER_LINES:
        audio = VOICE_ROOT / "Characters" / f"{stem}.wav"
        asset = PROJECT_ROOT / "Assets" / "ScriptableObjects" / "Characters" / f"{stem}.asset"
        if not asset.exists():
            asset = PROJECT_ROOT / "Assets" / "Resources" / "Characters" / f"{stem}.asset"
        jobs.append((f"skill:{stem}", line, voice, audio, asset, "skillVoice"))

    total = len(jobs)
    for index, (key, line, voice, audio, asset, field_name) in enumerate(jobs, 1):
        if not asset.is_file():
            raise FileNotFoundError(asset)
        print(f"[{index}/{total}] {key} ({voice}): {line}", flush=True)
        make_audio(client, key, line, voice, audio)
        ensure_unity_reference(asset, field_name, audio)

    print(f"Finished {total} clips. Unity references are assigned.", flush=True)


if __name__ == "__main__":
    main()
