"""床のテクスチャ (Albedo / Normal) を生成する。

コンクリート寄りの質感。上面と土台の 2 組を出力する。

上面 (Floor*.png)
    大きなムラと細かいざらつきの 2 層で density を作り、セル外周の目地を
    Normal に彫る。市松の明暗はマテリアルの Base Color が担当するため、
    テクスチャは Floor0 / Floor1 で共用できる。1 セル = 1 タイルなので
    シームレス化はしていない (境界には必ず目地が来る)。

土台 (FloorBase*.png)
    目地も気泡もない一枚岩。セルごとに複製された Cube に貼るため、
    1 unit ごとに同じ模様が反復する。反復が読み取られないよう、
    大きなムラは持たせず細かい粒だけで作り、上下左右シームレスにする。
"""

from pathlib import Path

import numpy as np
from PIL import Image

SIZE = 512
OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Images"

GROUT_WIDTH = 0.013  # 目地の幅 (UV)
GROUT_DEPTH = 0.16  # 目地の深さ
MOTTLE_STRENGTH = 0.09  # 大きなムラの濃さ (Albedo)
SPECKLE_STRENGTH = 0.028  # 細かいざらつきの濃さ (Albedo)
BUBBLE_COUNT = 16  # 気泡の数

BASE_TONE = 0.93  # 土台の明度。垂直面は上面より明るく出るぶんを引く
BASE_MOTTLE_STRENGTH = 0.030  # 土台のムラの濃さ (Albedo)
BASE_SPECKLE_STRENGTH = 0.032  # 土台のざらつきの濃さ (Albedo)

CONCRETE_BASE = np.array([0.93, 0.93, 0.92])
GROUT_COLOR = np.array([0.74, 0.74, 0.73])
BUBBLE_COLOR = np.array([0.66, 0.66, 0.65])

NORMAL_STRENGTH = 18.0


def smoothstep(edge0: float, edge1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def value_noise(shape: tuple[int, int], cells: int, rng, tileable=False) -> np.ndarray:
    """低解像度の乱数をバイリニア補間で拡大した値ノイズ。cells は格子数。

    tileable=True なら格子の端を反対側と同じ値にして、周期的なノイズにする。
    """
    grid = rng.random((cells + 1, cells + 1))
    if tileable:
        grid[-1, :] = grid[0, :]
        grid[:, -1] = grid[:, 0]
    img = Image.fromarray((grid * 255).astype(np.uint8), mode="L")
    img = img.resize((shape[1] + 1, shape[0] + 1), Image.BILINEAR)
    return np.asarray(img, dtype=np.float64)[: shape[0], : shape[1]] / 255.0


def layered_noise(
    shape: tuple[int, int], cells: list[int], rng, tileable=False
) -> np.ndarray:
    """複数スケールの値ノイズを重ね、平均 0 に揃えて返す。"""
    total = np.zeros(shape)
    weight = 1.0
    for c in cells:
        total += weight * value_noise(shape, c, rng, tileable)
        weight *= 0.5
    return total - total.mean()


def build() -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(20260820)

    v, u = np.meshgrid(
        np.linspace(0.0, 1.0, SIZE, endpoint=False) + 0.5 / SIZE,
        np.linspace(0.0, 1.0, SIZE, endpoint=False) + 0.5 / SIZE,
        indexing="ij",
    )
    v = 1.0 - v  # 画像は上が row 0、UV は上が v=1

    # --- 目地 -------------------------------------------------------------
    border = np.minimum(np.minimum(u, 1.0 - u), np.minimum(v, 1.0 - v))
    grout = 1.0 - smoothstep(GROUT_WIDTH * 0.55, GROUT_WIDTH, border)

    # --- ムラとざらつき ---------------------------------------------------
    mottle = layered_noise((SIZE, SIZE), [3, 7, 14], rng)
    speckle = layered_noise((SIZE, SIZE), [64, 128], rng)

    # --- 気泡 -------------------------------------------------------------
    bubbles = np.zeros((SIZE, SIZE))
    for _ in range(BUBBLE_COUNT):
        bx, by = rng.uniform(0.12, 0.88, 2)
        radius = rng.uniform(0.005, 0.013)
        dist = np.hypot(u - bx, v - by)
        bubbles = np.maximum(bubbles, 1.0 - smoothstep(radius * 0.5, radius, dist))

    # --- ハイトマップ -----------------------------------------------------
    height = np.full((SIZE, SIZE), 0.50)
    height += mottle * 0.030
    height += speckle * 0.007
    height -= GROUT_DEPTH * grout
    height -= 0.05 * bubbles

    # --- アルベド ---------------------------------------------------------
    albedo = np.broadcast_to(CONCRETE_BASE, (SIZE, SIZE, 3)).copy()
    albedo *= (1.0 + mottle * MOTTLE_STRENGTH)[..., None]
    albedo *= (1.0 + speckle * SPECKLE_STRENGTH)[..., None]
    albedo = albedo * (1.0 - grout[..., None]) + GROUT_COLOR * grout[..., None]
    albedo = albedo * (1.0 - bubbles[..., None]) + BUBBLE_COLOR * bubbles[..., None]

    return np.clip(albedo, 0.0, 1.0), height


def build_base() -> tuple[np.ndarray, np.ndarray]:
    """土台用。目地も気泡もなく、上下左右シームレス。"""
    rng = np.random.default_rng(20260821)

    # 1 unit ごとの反復が読み取られないよう、大きなムラは入れない
    mottle = layered_noise((SIZE, SIZE), [12, 24], rng, tileable=True)
    speckle = layered_noise((SIZE, SIZE), [64, 128], rng, tileable=True)

    height = np.full((SIZE, SIZE), 0.50)
    height += mottle * 0.016
    height += speckle * 0.006

    albedo = np.broadcast_to(CONCRETE_BASE * BASE_TONE, (SIZE, SIZE, 3)).copy()
    albedo *= (1.0 + mottle * BASE_MOTTLE_STRENGTH)[..., None]
    albedo *= (1.0 + speckle * BASE_SPECKLE_STRENGTH)[..., None]

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
    save(albedo, "FloorAlbedo.png")
    save(height_to_normal(height), "FloorNormal.png")

    albedo, height = build_base()
    save(albedo, "FloorBaseAlbedo.png")
    save(height_to_normal(height), "FloorBaseNormal.png")


if __name__ == "__main__":
    main()
