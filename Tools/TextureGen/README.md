# TextureGen

テクスチャ (Albedo / Normal) の生成スクリプト。

## セットアップ

```sh
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

## 実行

```sh
.venv/bin/python generate_crate.py  # 木箱  → Assets/Images/Crate*.png
.venv/bin/python generate_floor.py  # 床    → Assets/Images/Floor*.png
```

調整用の定数は各スクリプトの冒頭にまとめてある。

Unity 側では Normal の Texture Type を `Normal map` に設定すること。
