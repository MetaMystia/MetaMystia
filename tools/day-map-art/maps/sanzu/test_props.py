"""通用道具样张。"""
import sys

sys.path.insert(0, __file__.rsplit('/maps/', 1)[0])
from dayart import props as P
from dayart.sheet import contact_sheet

items = []
items += [(f'boulder{i}', P.boulder(w, h, i, moss=m)) for i, (w, h, m) in enumerate([(30, 22, 0), (46, 34, 0.35), (22, 16, 0), (64, 40, 0.3)])]
items += [(f'stack{i}', P.stone_stack(i + 3)) for i in range(6)]
items += [(f'higan{i}', P.higanbana_clump(i + 1)) for i in range(4)]
items += [('pin red', P.pinwheel('#d8333c')), ('pin green', P.pinwheel('#3f9a54')),
          ('sotoba', P.sotoba(1)), ('sotoba2', P.sotoba(2))]
items += [('lantern', P.stone_lantern(seed=1)), ('lantern lit', P.stone_lantern(True, seed=2)),
          ('chochin', P.paper_lantern()), ('chochin2', P.paper_lantern('#e9d4b0', None))]
items += [('tree', P.tree(1)), ('willow', P.willow(2)), ('bush', P.bush(3)), ('tuft', P.grass_tuft(4))]
contact_sheet(items, sys.argv[1], cols=6)
print('ok', len(items))
