"""画几张并把清醒面 / 癫狂面拼成一张预览：python peek.py C05 C10 …（输出 preview/_peek.png）。"""
import pathlib
import sys

from PIL import Image

import build

HERE = pathlib.Path(__file__).resolve().parent


def main(ids):
    build.load_types()
    rows = []
    for i in ids:
        build.build(i, HERE / 'preview')
        im = Image.open(HERE / 'preview' / f'{i}.png')
        rows.append(im.crop((0, 0, 1296, 480)))
    sheet = Image.new('RGB', (1296, 480 * len(rows)))
    for k, r in enumerate(rows):
        sheet.paste(r, (0, 480 * k))
    if len(rows) > 2:
        sheet = sheet.resize((int(1296 * 0.75), int(480 * len(rows) * 0.75)), Image.LANCZOS)
    sheet.save(HERE / 'preview' / '_peek.png')


if __name__ == '__main__':
    main(sys.argv[1:])
