"""木箱テクスチャ (Albedo / Normal) を生成する。

ハイトマップを組み立て、そこから Albedo と Normal を同時に出力する。
1 面 = 1 タイルなのでシームレス化はしていない (縁は枠が隠す)。
"""

from pathlib import Path

import numpy as np
from PIL import Image

SIZE = 512
OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Images"

PLANK_COUNT = 4
GROOVE_WIDTH = 0.008  # 板と板の間の溝の半幅 (UV)
FRAME_WIDTH = 0.085  # 外周枠の幅 (UV)
BRACE_WIDTH = 0.042  # バッテンの半幅 (UV)

WOOD_BASE = np.array([0.72, 0.53, 0.33])
WOOD_TRIM = np.array([0.60, 0.42, 0.25])  # 枠とバッテン
NAIL_COLOR = np.array([0.28, 0.26, 0.24])

PLANK_STEPS = np.array([1.00, 0.93, 1.06])  # 板の明度はこの段階だけを取る
PLANK_ORDER = [0, 2, 1, 0, 2, 1]  # 隣り合う板が同じ明度にならない並び
NORMAL_STRENGTH = 6.0


def smoothstep(edge0: float, edge1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def value_noise(shape: tuple[int, int], cells: tuple[int, int], rng) -> np.ndarray:
    """低解像度の乱数をバイリニア補間で拡大した値ノイズ。cells は (y, x) の格子数。"""
    grid = rng.random((cells[0] + 1, cells[1] + 1))
    img = Image.fromarray((grid * 255).astype(np.uint8), mode="L")
    img = img.resize((shape[1] + 1, shape[0] + 1), Image.BILINEAR)
    return np.asarray(img, dtype=np.float64)[: shape[0], : shape[1]] / 255.0


def wood_grain(shape: tuple[int, int], rng) -> np.ndarray:
    """縦方向に伸びた木目。x 方向に高周波、y 方向に低周波。"""
    grain = np.zeros(shape)
    for octave, weight in ((1, 0.55), (2, 0.28), (4, 0.17)):
        grain += weight * value_noise(shape, (4 * octave, 48 * octave), rng)
    return grain - grain.mean()


def build() -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(20260819)

    v, u = np.meshgrid(
        np.linspace(0.0, 1.0, SIZE, endpoint=False) + 0.5 / SIZE,
        np.linspace(0.0, 1.0, SIZE, endpoint=False) + 0.5 / SIZE,
        indexing="ij",
    )
    v = 1.0 - v  # 画像は上が row 0、UV は上が v=1

    # --- 縦板 -------------------------------------------------------------
    plank_pos = u * PLANK_COUNT
    plank_index = np.floor(plank_pos).astype(int)
    edge_dist = np.minimum(plank_pos % 1.0, 1.0 - plank_pos % 1.0) / PLANK_COUNT
    groove = 1.0 - smoothstep(GROOVE_WIDTH * 0.4, GROOVE_WIDTH, edge_dist)

    order = [PLANK_ORDER[i % len(PLANK_ORDER)] for i in range(PLANK_COUNT)]
    plank_shade = PLANK_STEPS[order]
    plank_lift = rng.uniform(-0.012, 0.012, PLANK_COUNT)

    # --- 外周枠 -----------------------------------------------------------
    border = np.minimum(np.minimum(u, 1.0 - u), np.minimum(v, 1.0 - v))
    frame = 1.0 - smoothstep(FRAME_WIDTH - 0.012, FRAME_WIDTH, border)

    # --- バッテン ---------------------------------------------------------
    inner = FRAME_WIDTH
    span = 1.0 - 2.0 * inner
    iu, iv = (u - inner) / span, (v - inner) / span
    diag_a = np.abs(iu - iv) / np.sqrt(2.0) * span
    diag_b = np.abs(iu + iv - 1.0) / np.sqrt(2.0) * span
    inside = (iu > -0.02) & (iu < 1.02) & (iv > -0.02) & (iv < 1.02)
    brace = np.where(
        inside,
        1.0 - smoothstep(BRACE_WIDTH - 0.010, BRACE_WIDTH, np.minimum(diag_a, diag_b)),
        0.0,
    )

    # --- 釘 ---------------------------------------------------------------
    nail_spots = [
        (inner * 1.6, inner * 1.6), (1.0 - inner * 1.6, inner * 1.6),
        (inner * 1.6, 1.0 - inner * 1.6), (1.0 - inner * 1.6, 1.0 - inner * 1.6),
        (0.5, 0.5),
        (0.5, inner * 0.5), (0.5, 1.0 - inner * 0.5),
        (inner * 0.5, 0.5), (1.0 - inner * 0.5, 0.5),
    ]
    nails = np.zeros((SIZE, SIZE))
    for nx, ny in nail_spots:
        dist = np.hypot(u - nx, v - ny)
        nails = np.maximum(nails, 1.0 - smoothstep(0.008, 0.012, dist))

    grain = wood_grain((SIZE, SIZE), rng)

    # --- ハイトマップ -----------------------------------------------------
    height = np.full((SIZE, SIZE), 0.50)
    height += plank_lift[np.clip(plank_index, 0, PLANK_COUNT - 1)]
    height -= 0.20 * groove
    height += grain * 0.018
    trim = np.maximum(frame, brace)
    height = np.maximum(height, 0.50 + 0.22 * trim + grain * 0.010)
    height += 0.05 * nails

    # --- アルベド ---------------------------------------------------------
    albedo = np.broadcast_to(WOOD_BASE, (SIZE, SIZE, 3)).copy()
    albedo *= plank_shade[np.clip(plank_index, 0, PLANK_COUNT - 1)][..., None]
    albedo = np.where(trim[..., None] > 0.5, WOOD_TRIM, albedo)
    albedo *= (1.0 + grain * 0.55)[..., None]
    albedo *= (1.0 - 0.55 * groove * (1.0 - trim))[..., None]  # 溝の影
    albedo *= (1.0 - 0.18 * smoothstep(0.55, 0.0, height))[..., None]  # 低い所を軽く暗く
    albedo = np.where(nails[..., None] > 0.5, NAIL_COLOR, albedo)

    return np.clip(albedo, 0.0, 1.0), height


def height_to_normal(height: np.ndarray) -> np.ndarray:
    dx = np.gradient(height, axis=1) * SIZE / NORMAL_STRENGTH
    dy = np.gradient(height, axis=0) * SIZE / NORMAL_STRENGTH
    # 法線 = (-dh/du, -dh/dv, 1)。row は v の逆向きなので dy の符号はそのまま。
    normal = np.stack([-dx, dy, np.ones_like(height)], axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    return normal * 0.5 + 0.5


def save(array: np.ndarray, name: str) -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUT_DIR / name
    Image.fromarray((np.clip(array, 0.0, 1.0) * 255).round().astype(np.uint8)).save(path)
    print(f"wrote {path}")


def main() -> None:
    albedo, height = build()
    save(albedo, "SubjectBoxAlbedo.png")
    save(height_to_normal(height), "SubjectBoxNormal.png")


if __name__ == "__main__":
    main()
