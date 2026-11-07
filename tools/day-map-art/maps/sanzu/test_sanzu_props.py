"""三途川道具样张。用法：python test_sanzu_props.py out.png [字体路径]"""
import sys

sys.path.insert(0, __file__.rsplit('/maps/', 1)[0])
sys.path.insert(0, __file__.rsplit('/', 1)[0])
import props as S
from dayart.sheet import contact_sheet

font = sys.argv[2] if len(sys.argv) > 2 else None
items = [('jizo', S.jizo(1)), ('jizo row', S.jizo_row(2)), ('boat', S.boat()), ('sign', S.stone_sign(font)),
         ('bell', S.bell_frame()), ('shelter', S.shelter()), ('candy', S.stall('candy', 1, font)),
         ('goldfish', S.stall('goldfish', 2, font)), ('sotoba', S.stall('sotoba', 3, font)),
         ('fortune', S.stall('fortune', 4, font)), ('gate beam', S.gate_beam(300, font)), ('post', S.gate_posts()[0]),
         ('kimono tree', S.kimono_tree()), ('sakura', S.purple_sakura()), ('signpost', S.signpost(font)),
         ('skeleton', S.skeleton()), ('rack', S.fish_rack()), ('urumi', S.urumi_stall(0, font)), ('barrel', S.barrel()),
         ('crate', S.crate()), ('nets', S.net_poles()), ('hitodama', S.hitodama()), ('float', S.floating_lantern()),
         ('board', S.contest_board(font)), ('pennants', S.pennant_line(280)), ('rail', S.bridge_rail(240, 48))]
contact_sheet(items, sys.argv[1], cols=5, scale=2)
print('ok', len(items))
