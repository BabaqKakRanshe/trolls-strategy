"""
Локация 1 (колония) в стиле арены: окружение вокруг поля стройки 14x14.

  plot.py     геометрия поля колонии (EconomyConfig 14x14, клетка 1 м) и стартовые здания сцены
  assets.py   новые ассеты колонии: водяная мельница, штольня, стога, детали входа зданий
  fields.py   делянки полей (меши поверх террасы)
  layout.py   композиция Colony_Meadow: террасы, река, водопад, лес, фон, реквизит, камера

Рельеф, вода и водопады — общий инструмент vitaria_arena/terrain.py. Сборка, превью и экспорт —
build_colony.py рядом с build_arena.py.
"""
import importlib

MODULES = ["assets"]


def load(reload=False):
    subs = ["plot", "fields"] + MODULES + ["layout"]
    mods = {}
    for n in subs:
        m = importlib.import_module(__name__ + "." + n)
        if reload:
            m = importlib.reload(m)
        mods[n] = m
    return mods


def asset_entries(reload=False):
    mods = load(reload)
    out = []
    for n in MODULES:
        out.extend(mods[n].ASSETS)
    return out
