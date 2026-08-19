# TextureGen

木箱テクスチャ (Albedo / Normal) の生成スクリプト。

## セットアップ

```sh
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

## 実行

```sh
.venv/bin/python generate_crate.py
```

`Assets/Images/SubjectBoxAlbedo.png` と `SubjectBoxNormal.png` を出力する。

Unity 側では Normal の Texture Type を `Normal map` に設定すること。
