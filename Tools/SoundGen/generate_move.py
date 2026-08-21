"""プレイヤーの移動音 (PlayerMove.mp3) を生成する。

音のモデル
    打撃 = 接触（短い励振ノイズ） * 叩かれた側の共振（減衰する正弦波の和）
    そこへ床の低い鳴りと初期反射を足し、薄い反響で包む。

このゲームの音として決まったこと
    本体 167/189Hz
        近接した 2 本にすることで音程をぼかす。1 本だと木魚になる。
        175Hz だとイヤホンで跳ねて聞こえ、140Hz だとノート PC の
        スピーカーで聞き取れない。167Hz がその両立点。
    接触 75ms
        アタックの鈍らせ方。数 ms だと硬く鋭くなり、200ms を超えると
        安物マイクで録ったようなこもり方になる。
    初期反射 1.5/3/6ms
        音の道のり 0.5〜2m ぶん。あの浮島の広さに合う。10ms を超える
        反射を入れると室内の音になり、跳ね返りとして聞こえてしまう。
    260Hz から上を -24dB
        素のままだと石を叩く音になる。柔らかい体の音にするため上を削る。

イヤホンとスピーカーで聞こえ方が違うのは残るが、これは市販の音源でも
同じことなので詰めていない。
"""

import subprocess
import tempfile
import wave
from pathlib import Path

import numpy as np

SR = 44100
OUT_PATH = Path(__file__).resolve().parents[2] / "Assets" / "Audio" / "PlayerMove.mp3"

BODY_FREQ = 167.0  # 本体の音程 (Hz)
BODY_RATIO = 1.131  # 2 本目の比。近すぎると音程が立ち、離れると 2 つの音になる
BODY_DECAY = (0.028, 0.022)  # 2 本それぞれの減衰 (s)
BODY_LEVEL = (1.00, 0.55)

CONTACT_MS = 75.0  # 接触の長さ。小さいほど硬く鋭い
EXCITE_FC = BODY_FREQ * 2.06  # 励振ノイズのカットオフ。接触の柔らかさ
BODY_DUR = 0.18  # 本体を計算する長さ (s)
SEED = 0  # 励振ノイズの並び。変えると手触りが変わる

REFLECTIONS = [  # (遅延 s, 音量, カットオフ Hz)。遅い反射ほど暗い
    (0.0015, 0.45, 8000 * 0.42),
    (0.0030, 0.30, 6000 * 0.42),
    (0.0060, 0.18, 4000 * 0.42),
]

# 床が後から鳴る成分。本体より 33dB 下で単体では聞こえないが、採用時の状態
BLOOM_SUBS = [(76.0, 0.28, 0.18), (140.0, 0.12, 0.10)]  # (Hz, 減衰 s, 音量)
BLOOM_RISE_MS = 12.0  # 立ち上がりを遅らせる。速いと太鼓になる

ROOM_MIX = 0.07  # 拡散反響の量。多いと体育館になる
ROOM_TAIL = 0.45  # 反響の長さ (s)
ROOM_FC = 1500.0  # 反響のカットオフ。明るいと屋内になる

HIGHPASS_FC = 40.0  # これより下は再生できる機器が限られるので捨てる
SHELF_FC = BODY_FREQ * 1.49  # ここから上を落とす
SHELF_GAIN = 0.063  # -24dB
PEAK = 0.89  # 書き出しの頂点

TOTAL_DUR = 0.90  # 計算する全長 (s)。実際は無音を切り詰めて書き出す
TRIM_FLOOR_DB = -60.0  # これを下回ったところで切る


def fftconv(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    length = len(a) + len(b) - 1
    size = 1 << (length - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(a, size) * np.fft.rfft(b, size), size)[:length]


def lowpass(x: np.ndarray, fc: float, poles: int = 2) -> np.ndarray:
    """1 次のローパスを重ねる。位相をいじらないので立ち上がりが濁らない。"""
    coef = 1 - np.exp(-2 * np.pi * fc / SR)
    y = x
    for _ in range(poles):
        out = np.empty_like(y)
        state = 0.0
        for i in range(len(y)):
            state += coef * (y[i] - state)
            out[i] = state
        y = out
    return y


def highpass(x: np.ndarray, fc: float, poles: int = 1) -> np.ndarray:
    rc = 1.0 / (2 * np.pi * fc)
    coef = rc / (rc + 1.0 / SR)
    y = x
    for _ in range(poles):
        out = np.empty_like(y)
        prev_in = 0.0
        prev_out = 0.0
        for i in range(len(y)):
            prev_out = coef * (prev_out + y[i] - prev_in)
            prev_in = y[i]
            out[i] = prev_out
        y = out
    return y


def shelf(x: np.ndarray, fc: float, gain: float) -> np.ndarray:
    """fc から上だけを gain 倍にする。下は素通し。"""
    low = lowpass(x, fc, 1)
    return low + gain * (x - low)


def excitation(n: int, contact_ms: float, fc: float, seed: int) -> np.ndarray:
    """接触そのもの。柔らかい物ほど接触が長く、高域が出ない。"""
    rng = np.random.default_rng(seed)
    e = rng.standard_normal(n)
    e *= np.exp(-(np.arange(n) / SR) / (contact_ms / 3000))
    e = lowpass(e, fc, 2)
    ramp = int(SR * 0.0003)
    e[:ramp] *= np.linspace(0.0, 1.0, ramp)
    return e


def impact() -> np.ndarray:
    """接触と共振を畳み込んだ本体。"""
    n = int(SR * BODY_DUR)
    t = np.arange(n) / SR
    modes = [
        (BODY_FREQ, BODY_DECAY[0], BODY_LEVEL[0]),
        (BODY_FREQ * BODY_RATIO, BODY_DECAY[1], BODY_LEVEL[1]),
    ]
    ir = np.zeros(n)
    for freq, decay, level in modes:
        ir += level * np.exp(-t / decay) * np.sin(2 * np.pi * freq * t)
    sig = fftconv(excitation(n, CONTACT_MS, EXCITE_FC, SEED), ir)[:n]
    fade = int(SR * 0.01)
    sig[-fade:] *= np.linspace(1.0, 0.0, fade)
    return sig


def early(x: np.ndarray, pad: float = 0.12) -> np.ndarray:
    """初期反射。遅延した複製を重ねて距離を作る。"""
    out = np.zeros(len(x) + int(SR * pad))
    out[: len(x)] = x
    for delay, gain, fc in REFLECTIONS:
        tap = lowpass(x, fc, 1) * gain
        offset = int(SR * delay)
        m = min(len(tap), len(out) - offset)
        out[offset : offset + m] += tap[:m]
    return out


def bloom(n: int) -> np.ndarray:
    """床が後から鳴る成分。立ち上がりが遅いので打撃にならない。"""
    t = np.arange(n) / SR
    rise = 1 - np.exp(-t / (BLOOM_RISE_MS / 1000))
    out = np.zeros(n)
    for freq, decay, level in BLOOM_SUBS:
        out += level * np.sin(2 * np.pi * freq * t) * rise * np.exp(-t / decay)
    return out


def room(x: np.ndarray, seed: int = 5) -> np.ndarray:
    """拡散した反響。屋外なので薄く、暗く。"""
    n = int(SR * ROOM_TAIL)
    rng = np.random.default_rng(seed)
    ir = rng.standard_normal(n) * np.exp(-(np.arange(n) / SR) / (ROOM_TAIL * 0.25))
    ir = lowpass(ir, ROOM_FC, 2)
    ir = np.concatenate([np.zeros(int(SR * 0.008)), ir])
    ir /= np.sqrt(np.sum(ir**2))
    wet = fftconv(x, ir)
    dry = np.zeros(len(wet))
    dry[: len(x)] = x
    scale = np.max(np.abs(x)) / max(np.max(np.abs(wet)), 1e-9)
    return (1 - ROOM_MIX) * dry + ROOM_MIX * wet * scale


def normalize(x: np.ndarray, peak: float = PEAK) -> np.ndarray:
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def trim(x: np.ndarray) -> np.ndarray:
    """末尾の無音を落とし、切り口が鳴らないようフェードする。"""
    threshold = np.max(np.abs(x)) * 10 ** (TRIM_FLOOR_DB / 20)
    voiced = np.where(np.abs(x) > threshold)[0]
    x = x[: min(voiced[-1] + int(SR * 0.01), len(x))].copy()
    fade = int(SR * 0.005)
    x[-fade:] *= np.linspace(1.0, 0.0, fade)
    return x


def build() -> np.ndarray:
    n = int(SR * TOTAL_DUR)
    mid = early(impact())
    out = np.zeros(n)
    m = min(len(mid), n)
    out[:m] = mid[:m]
    out += bloom(n)
    out = room(out)
    out = shelf(highpass(out, HIGHPASS_FC, 2), SHELF_FC, SHELF_GAIN)
    return trim(normalize(out))


def save(x: np.ndarray, path: Path) -> None:
    """mp3 で書き出す。.meta を残すため Unity 側の参照は切れない。"""
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = (normalize(x) * 32767).astype("<i2")
    with tempfile.NamedTemporaryFile(suffix=".wav") as tmp:
        with wave.open(tmp.name, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(pcm.tobytes())
        subprocess.run(
            ["ffmpeg", "-y", "-i", tmp.name, "-codec:a", "libmp3lame",
             "-b:a", "192k", "-ac", "1", str(path)],
            check=True, capture_output=True,
        )
    print(f"wrote {path}")


def main() -> None:
    save(build(), OUT_PATH)


if __name__ == "__main__":
    main()
