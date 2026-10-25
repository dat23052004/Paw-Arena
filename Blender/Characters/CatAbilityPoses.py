"""Authored body gestures for the eleven abilities in Paw Arena GDD v1.1.

These are animation interpretations, not gameplay, effect meshes or timings.
"""
import math

CLIPS = [
    ('Ability_Dash', 18, False, 8),
    ('Ability_NineLives_FaceUp', 36, False, 22),
    ('Ability_NineLives_FaceDown', 36, False, 22),
    ('Ability_Hiss', 18, False, 8),
    ('Ability_Pounce_Start', 12, False, 8),
    ('Ability_Pounce_Air', 18, True, 7),
    ('Ability_Pounce_Land', 15, False, 4),
    ('Ability_Anchor_Start', 18, False, 14),
    ('Ability_Anchor_Hold', 48, True, 14),
    ('Ability_Anchor_End', 18, False, 10),
    ('Ability_Stomp', 24, False, 10),
    ('Ability_Catnip', 24, False, 12),
    ('Ability_FluffUp', 27, False, 13),
    ('Ability_Hairball', 24, False, 11),
    ('Ability_SharkAttack', 33, False, 17),
    ('Ability_WindShurikens', 33, False, 10),
]

MARKERS = {
    'Ability_Dash': [('DASH POSE GUIDE', 5)],
    'Ability_Hiss': [('HISS GUIDE', 8)],
    'Ability_Pounce_Start': [('TAKEOFF GUIDE', 10)],
    'Ability_Pounce_Land': [('LANDING GUIDE', 4)],
    'Ability_Stomp': [('GROUND CONTACT GUIDE', 14)],
    'Ability_Catnip': [('ENERGIZE GUIDE', 12)],
    'Ability_FluffUp': [('POWER UP GUIDE', 13)],
    'Ability_Hairball': [('HAIRBALL RELEASE GUIDE', 11)],
    'Ability_SharkAttack': [('SUMMON GUIDE', 17)],
    'Ability_WindShurikens': [('FIRST FAN GUIDE', 10), ('SECOND FAN GUIDE', 21)],
}


def pose_for(name, t, base):
    p, v, keys = base.pose(), base.variant, base.core.keys
    w = math.sin(math.tau * t)
    if name == 'Ability_Dash':
        prep = v(p, hips=(0, .01, -.09), spine=(9, 0, 0), head=(-7, 0, 0),
                 left_hand=(.61, .10, .68), right_hand=(-.61, .10, .68), tail_pitch=5)
        dash = v(p, hips=(0, -.02, -.06), spine=(17, 0, -3), head=(-13, 0, 3),
                 left_hand=(.60, .28, .91), right_hand=(-.60, .28, .91),
                 left_foot=(.24, -.09, .16), right_foot=(-.24, .12, .20),
                 ears=(-8, 8), tail_pitch=-4)
        return keys(t, [(0,p), (.17,prep), (.30,dash), (.67,dash), (1,p)])
    if name.startswith('Ability_NineLives_'):
        # Fast recovery variants; physics chooses the appropriate orientation.
        progress = min(1, max(0, (t - .04) / .84))
        return base.recover_pose(progress, name.endswith('FaceUp'))
    if name == 'Ability_Hiss':
        coil = v(p, hips=(0,.01,-.075), spine=(-5,0,0), head=(-6,0,0), tail_pitch=5)
        hiss = v(p, hips=(0,-.01,-.05), spine=(12,0,0), head=(4,0,0),
                 left_hand=(.77,-.20,1.12), right_hand=(-.77,-.20,1.12),
                 ears=(-18,18), tail_pitch=8, tail_sway=3)
        return keys(t, [(0,p), (.20,coil), (7/18,hiss), (.62,hiss), (1,p)])
    if name.startswith('Ability_Pounce_'):
        crouch = v(p, hips=(0,.01,-.115), spine=(13,0,0), head=(-10,0,0),
                   left_hand=(.63,-.05,.64), right_hand=(-.63,-.05,.64),
                   left_foot=(.239,0,.16), right_foot=(-.239,0,.16), tail_pitch=6)
        air = v(p, hips=(0,0,-.01), spine=(12,0,0), head=(-7,0,0),
                left_hand=(.50,-.62,1.23), right_hand=(-.50,-.62,1.23),
                left_foot=(.26,.11,.24), right_foot=(-.26,.11,.24),
                body_rotation=(18,0,0), ears=(-5,5), tail_pitch=-5)
        if name.endswith('Start'):
            return keys(t, [(0,p), (.45,crouch), (1,air)])
        if name.endswith('Air'):
            return v(air, head=(-7+w,0,0), tail_sway=2*w, tail_phase=math.tau*t)
        land = v(crouch, left_hand=(.66,-.40,.81), right_hand=(-.66,-.40,.81),
                 left_foot=(.239,0,.16), right_foot=(-.239,0,.16), ears=(-6,6))
        return keys(t, [(0,air), (3/15,land), (1,p)])
    if name.startswith('Ability_Anchor_'):
        anchor = v(p, hips=(0,.008,-.105), spine=(5,0,0), head=(-4,0,0),
                   left_foot=(.29,0,.15), right_foot=(-.29,0,.15),
                   left_hand=(.66,-.28,.84), right_hand=(-.66,-.28,.84),
                   ears=(-4,4), tail_pitch=5)
        if name.endswith('Start'):
            return keys(t, [(0,p), (.72,anchor), (1,anchor)])
        if name.endswith('End'):
            return keys(t, [(0,anchor), (1,p)])
        return v(anchor, head=(-4+.6*w,0,0), tail_sway=1.5*w, tail_phase=math.tau*t,
                 left_hand=(.66,-.28,.84+.004*w), right_hand=(-.66,-.28,.84+.004*w))
    if name == 'Ability_Stomp':
        up = v(p, hips=(-.045,0,-.055), spine=(-5,0,-3), head=(3,0,3),
               left_foot=(.24,-.10,.29), right_foot=(-.239,0,.155),
               left_hand=(.67,-.03,1.12), right_hand=(-.64,-.06,1.0), tail_pitch=-4)
        impact = v(p, hips=(0,-.005,-.105), spine=(11,0,0), head=(5,0,0),
                   left_foot=(.26,-.08,.165), right_foot=(-.239,0,.16), left_hand=(.72,-.15,.73),
                   right_hand=(-.72,-.15,.73), ears=(-8,8), tail_pitch=6)
        return keys(t, [(0,p), (9/24,up), (13/24,impact), (.72,impact), (1,p)])
    if name == 'Ability_Catnip':
        prep = v(p, hips=(0,0,-.085), spine=(5,0,0), head=(5,0,0), tail_pitch=4)
        perk = v(p, hips=(0,0,-.015), spine=(-5,0,0), head=(-7,0,0),
                 left_hand=(.62,-.27,1.13), right_hand=(-.62,-.27,1.13),
                 ears=(6,-6), tail_sway=7)
        return keys(t, [(0,p), (.25,prep), (11/24,perk), (.65,v(perk,tail_sway=-4)), (1,p)])
    if name == 'Ability_FluffUp':
        gather = v(p, hips=(0,0,-.07), spine=(5,0,0), head=(4,0,0),
                   left_hand=(.43,-.43,1.12), right_hand=(-.43,-.43,1.12), tail_pitch=4)
        proud = v(p, hips=(0,0,-.025), spine=(-7,0,0), head=(-5,0,0),
                  left_hand=(.82,-.10,1.19), right_hand=(-.82,-.10,1.19),
                  ears=(7,-7), tail_pitch=8)
        return keys(t, [(0,p), (.25,gather), (12/27,proud), (.75,proud), (1,p)])
    if name == 'Ability_Hairball':
        inhale = v(p, spine=(-7,0,0), head=(-12,0,0),
                   left_hand=(.52,-.29,.90), right_hand=(-.52,-.29,.90), tail_pitch=-3)
        spit = v(inhale, hips=(0,-.01,-.06), spine=(16,0,0), head=(12,0,0),
                 left_hand=(.64,-.31,.83), right_hand=(-.64,-.31,.83), ears=(-7,7), tail_pitch=6)
        return keys(t, [(0,p), (.29,inhale), (10/24,spit), (.56,spit), (1,p)])
    if name == 'Ability_SharkAttack':
        windup = v(p, hips=(0,.005,-.06), spine=(-4,0,-5), head=(-3,0,5),
                   right_hand=(-.55,-.12,1.61), right_pole=(-.82,.05,1.22), tail_sway=5)
        command = v(p, hips=(0,-.01,-.06), spine=(8,0,4), head=(-5,0,-4),
                    right_hand=(-.42,-.67,1.04), left_hand=(.64,-.06,.74),
                    ears=(-3,3), tail_sway=-4)
        return keys(t, [(0,p), (.32,windup), (16/33,command), (.7,command), (1,p)])
    if name == 'Ability_WindShurikens':
        wind = v(p, hips=(0,0,-.05), spine=(-2,0,-7), head=(0,0,5),
                 left_hand=(.66,.05,1.21), right_hand=(-.45,-.38,1.05), tail_sway=5)
        left = v(p, hips=(0,0,-.06), spine=(5,0,6), head=(-3,0,-4),
                 left_hand=(.45,-.65,1.21), right_hand=(-.65,-.03,1.17), tail_sway=-4)
        right = v(p, hips=(0,0,-.06), spine=(5,0,-6), head=(-3,0,4),
                  right_hand=(-.45,-.65,1.21), left_hand=(.66,-.10,1.00), tail_sway=4)
        return keys(t, [(0,p), (5/33,wind), (9/33,left), (15/33,v(wind,spine=(-2,0,6))),
                        (20/33,right), (.79,right), (1,p)])
    raise ValueError(name)
