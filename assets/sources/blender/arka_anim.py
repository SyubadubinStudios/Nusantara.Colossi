# Arka's gameplay clips, authored procedurally and baked one key per frame (60 fps).
# Used by process_arka.py (full rebuild) and reanimate_arka.py (re-author clips on arka.blend).
#
# Every pose is written in *armature rest space* and converted to each bone's local frame, so the
# conventions below hold for every bone regardless of its roll (character faces Blender +Y, its
# right side is +X, up is +Z):
#   spine / neck / head / hips  Rx(-a) leans forward, Rz turns left, Ry(+a) bends to the right
#   upper_leg                   Rx(+a) swings the leg forward (hip flexion), Ry spreads (see leg())
#   lower_leg                   Rx(-a) bends the knee
#   foot                        Rx(-a) points the toe down
#   upper_arm                   arm(): `fwd` raises the hanging arm forward (90 = horizontal,
#                               170 = overhead), `out` lifts it sideways, `bend` folds the elbow
# Game names (CharacterVisual.Pick): Idle Run Sprint Jump Fall Climb ClimbIdle Hang Stab StabRelease
# Dodge Swim Death Ride Glide. Loops start and end on the same pose; one-shots (Dodge, StabRelease,
# Death) end on a pose the game holds.
import math
import bpy
from mathutils import Quaternion, Vector, Matrix

FPS = 60
TAU = math.tau


def rx(d): return Quaternion((1, 0, 0), math.radians(d))
def ry(d): return Quaternion((0, 1, 0), math.radians(d))
def rz(d): return Quaternion((0, 0, 1), math.radians(d))


def smooth(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
def lerp(a, b, t): return a + (b - a) * t
def wave(t, phase=0.0): return math.sin(TAU * (t + phase))    # one cycle over the clip


def key_lerp(t, keys):
    """Piecewise smoothstep through [(t, value), ...]; t in 0..1."""
    if t <= keys[0][0]: return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1:
            return lerp(v0, v1, smooth((t - t0) / max(1e-6, t1 - t0)))
    return keys[-1][1]


class Pose(dict):
    """bone name -> armature-space rotation; '_hips_loc' -> armature-space offset."""

    def rot(self, bone, q):
        self[bone] = q @ self.get(bone, Quaternion())
        return self


ARM_DOWN = 70.0   # set by build_all from the rig: rotation that drops the A-pose arm to the side


def arm(p, side, fwd=0.0, out=0.0, bend=0.0, wrist=0.0):
    s = 1 if side == "R" else -1
    # drop to hanging, lift sideways, then swing forward/up around the shoulder's X axis
    p.rot("upper_arm." + side, rx(fwd) @ ry(s * (ARM_DOWN - out)))
    p.rot("lower_arm." + side, rz(s * bend))
    if wrist: p.rot("hand." + side, rz(s * wrist))


def leg(p, side, fwd=0.0, knee=0.0, foot=0.0, out=0.0):
    s = 1 if side == "R" else -1
    p.rot("upper_leg." + side, rx(fwd) @ ry(-s * out))
    p.rot("lower_leg." + side, rx(-knee))
    p.rot("foot." + side, rx(-foot))


def torso(p, lean=0.0, twist=0.0, side=0.0, chest=None, head=0.0, head_turn=0.0):
    p.rot("spine", rz(twist * .5) @ ry(side * .5) @ rx(-lean * .5))
    p.rot("chest", rz(twist * .5) @ ry(side * .5) @ rx(-(lean * .5 if chest is None else chest)))
    p.rot("neck", rx(head * .4) @ rz(head_turn * .5))
    p.rot("head", rx(head * .6) @ rz(head_turn * .5))


def hips(p, lift=0.0, fwd=0.0, side=0.0, pitch=0.0, yaw=0.0, roll=0.0):
    p["_hips_loc"] = Vector((side, fwd, lift))
    p.rot("hips", rz(yaw) @ ry(roll) @ rx(-pitch))


# ---------------------------------------------------------------- clips (t in 0..1)

def idle(t):
    p = Pose()
    b = wave(t)                                   # one slow breath
    hips(p, lift=-.01 + .006 * b, side=.012 * wave(t, .25), roll=.8 * wave(t, .25))
    torso(p, lean=2 + 1.2 * b, chest=-1.5 * b, head=-2 + 1.5 * wave(t, .1), head_turn=6 * wave(t * .5 + .1))
    arm(p, "R", fwd=4 + 2 * b, out=8, bend=14 + 3 * b)
    arm(p, "L", fwd=4 + 2 * b, out=8, bend=14 + 3 * b)
    leg(p, "R", fwd=2, knee=4, out=3)
    leg(p, "L", fwd=-1, knee=2, out=4)
    return p


def _locomotion(t, amp, lean, arm_amp, knee_amp, bounce, elbow):
    p = Pose()
    c = wave(t)                                   # right leg forward at c = 1
    c2 = math.cos(TAU * t * 2)                    # two bounces per cycle
    hips(p, lift=-.04 - bounce * c2, yaw=-8 * c * amp / 40, roll=3 * c)
    torso(p, lean=lean, twist=10 * c * amp / 40, head=lean * .6, side=-2 * c)
    for side, sgn in (("R", 1), ("L", -1)):
        ph = sgn * c
        swing = amp * ph
        # knee folds most while the leg swings through (passing position) and on contact absorbs a little
        passing = max(0.0, -math.sin(TAU * t + (0 if sgn > 0 else math.pi)) )
        knee = 12 + knee_amp * passing ** 1.5 + 8 * max(0.0, ph)
        leg(p, side, fwd=swing + 8, knee=knee, foot=-12 * passing + 10 * max(0.0, ph))
        arm(p, side, fwd=-arm_amp * ph + 6, out=10, bend=elbow + 10 * max(0.0, -ph))
    return p


def run(t): return _locomotion(t, amp=38, lean=12, arm_amp=42, knee_amp=80, bounce=.035, elbow=78)
def sprint(t): return _locomotion(t, amp=52, lean=20, arm_amp=62, knee_amp=105, bounce=.05, elbow=88)


def jump(t):
    p = Pose()
    w = .5 + .5 * wave(t)
    hips(p, lift=.06, pitch=4)
    torso(p, lean=8, head=-6)
    arm(p, "R", fwd=110 + 15 * w, out=25, bend=40)
    arm(p, "L", fwd=95 + 15 * w, out=30, bend=35)
    leg(p, "R", fwd=55, knee=85, foot=15)
    leg(p, "L", fwd=20, knee=55, foot=20)
    return p


def fall(t):
    p = Pose()
    a, b = wave(t), wave(t, .3)
    hips(p, pitch=-6)
    torso(p, lean=-8, head=10, side=4 * a)
    arm(p, "R", fwd=60 + 25 * a, out=75 + 10 * b, bend=30)
    arm(p, "L", fwd=60 - 25 * a, out=75 - 10 * b, bend=30)
    leg(p, "R", fwd=25 + 20 * b, knee=30 + 15 * a, out=12)
    leg(p, "L", fwd=10 - 20 * b, knee=45 - 15 * a, out=12)
    return p


def _wall(p, lift=0.0):
    # Chest to the surface (the surface is in front, +Y): lean in, head tipped back to look up.
    hips(p, fwd=.12, lift=lift, pitch=6)
    torso(p, lean=4, head=14)


def climb(t):
    p = Pose()
    c = wave(t)
    _wall(p, lift=.03 * math.cos(TAU * t * 2))
    torso(p, side=6 * c)
    for side, sgn in (("R", 1), ("L", -1)):
        reach = .5 + .5 * sgn * c                 # 1 = hand at its highest
        arm(p, side, fwd=lerp(125, 172, reach), out=lerp(28, 12, reach), bend=lerp(85, 15, reach))
        step = 1 - reach                          # opposite foot steps up
        leg(p, side, fwd=lerp(10, 62, step), knee=lerp(20, 95, step), foot=-10, out=12)
    return p


def climb_idle(t):
    p = Pose()
    b = wave(t)
    _wall(p, lift=.008 * b)
    arm(p, "R", fwd=160 + 2 * b, out=16, bend=32)
    arm(p, "L", fwd=148 - 2 * b, out=20, bend=48)
    leg(p, "R", fwd=42, knee=75, foot=-8, out=12)
    leg(p, "L", fwd=18, knee=34, foot=-8, out=12)
    torso(p, side=1.5 * b)
    return p


def hang(t):
    p = Pose()
    s = wave(t)                                   # pendulum sway
    hips(p, pitch=-3 * s, roll=2 * wave(t, .25))
    torso(p, lean=-2 * s, head=10)
    arm(p, "R", fwd=172, out=14, bend=8)
    arm(p, "L", fwd=172, out=14, bend=8)
    leg(p, "R", fwd=6 + 10 * s, knee=14 + 6 * s, foot=-25)
    leg(p, "L", fwd=-2 + 10 * s, knee=22 - 6 * s, foot=-25)
    return p


def stab(t):
    # Charging: blade raised two-handed overhead, back arched, trembling with the build-up.
    p = Pose()
    tremble = .6 * math.sin(TAU * t * 9)
    hips(p, lift=-.06, pitch=-4)
    torso(p, lean=-12 + tremble, head=6)
    arm(p, "R", fwd=168 + tremble, out=8, bend=62)
    arm(p, "L", fwd=164 + tremble, out=14, bend=72)
    leg(p, "R", fwd=28, knee=50, foot=-6, out=8)
    leg(p, "L", fwd=-12, knee=30, foot=-4, out=8)
    return p


def stab_release(t):
    # One-shot: drive the blade down in front, the body follows through into a crouch.
    a = smooth(min(1.0, t / .35))
    p = Pose()
    hips(p, lift=lerp(-.06, -.16, a), pitch=lerp(-4, 10, a))
    torso(p, lean=lerp(-12, 30, a), head=lerp(6, -18, a))
    arm(p, "R", fwd=lerp(168, 40, a), out=8, bend=lerp(62, 12, a))
    arm(p, "L", fwd=lerp(164, 48, a), out=12, bend=lerp(72, 20, a))
    leg(p, "R", fwd=lerp(28, 48, a), knee=lerp(50, 80, a), foot=-6, out=8)
    leg(p, "L", fwd=lerp(-12, -6, a), knee=lerp(30, 60, a), foot=-4, out=8)
    return p


def dodge(t):
    # One-shot forward roll: crouch, tuck, a full turn about the hips, rise to a crouch.
    p = Pose()
    turn = 360 * smooth((t - .12) / .66)
    tuck = key_lerp(t, [(0, 0), (.15, 1), (.72, 1), (1, .25)])
    hips(p, lift=key_lerp(t, [(0, 0), (.15, -.25), (.45, -.38), (.75, -.25), (1, -.08)]), pitch=turn)
    torso(p, lean=lerp(8, 45, tuck), head=lerp(-5, -35, tuck))
    for side in ("R", "L"):
        arm(p, side, fwd=lerp(20, 70, tuck), out=lerp(12, 22, tuck), bend=lerp(30, 95, tuck))
        leg(p, side, fwd=lerp(10, 110, tuck), knee=lerp(20, 140, tuck), foot=lerp(0, 20, tuck), out=6)
    return p


def swim(t):
    # Breaststroke-crawl hybrid that reads well from behind: body flat, alternating arm pulls, flutter kick.
    p = Pose()
    c = wave(t)
    hips(p, lift=-.2, pitch=72, roll=8 * c)
    torso(p, lean=4, head=48, side=-3 * c)
    for side, sgn in (("R", 1), ("L", -1)):
        u = (t + (0 if sgn > 0 else .5)) % 1.0  # each arm half a cycle apart
        # reach forward (fwd ~170 relative to the flat body) then pull back to the hip
        fwd = key_lerp(u, [(0, 170), (.55, 20), (.75, 60), (1, 170)])
        arm(p, side, fwd=fwd, out=key_lerp(u, [(0, 12), (.55, 18), (.75, 60), (1, 12)]),
            bend=key_lerp(u, [(0, 10), (.3, 70), (.55, 20), (.8, 90), (1, 10)]))
        kick = math.sin(TAU * t * 3 + (0 if sgn > 0 else math.pi))
        leg(p, side, fwd=-4 + 14 * kick, knee=12 + 10 * max(0.0, kick), foot=-40)
    return p


def death(t):
    # One-shot: knees buckle, Arka topples backwards and comes to rest on the ground.
    p = Pose()
    buckle = smooth(t / .3)
    fall_ = smooth((t - .2) / .45)
    settle = smooth((t - .65) / .35)
    hips(p, lift=lerp(lerp(0, -.35, buckle), -.72, fall_), fwd=lerp(0, -.18, fall_), pitch=lerp(lerp(0, 12, buckle), -88, fall_), roll=6 * fall_)
    torso(p, lean=lerp(lerp(0, 25, buckle), -4, fall_), head=lerp(lerp(0, -20, buckle), 8 + 6 * settle, fall_), head_turn=22 * settle)
    arm(p, "R", fwd=lerp(lerp(0, 30, buckle), 130, fall_), out=lerp(10, 60, fall_), bend=lerp(30, 40 - 20 * settle, fall_))
    arm(p, "L", fwd=lerp(lerp(0, 20, buckle), 115, fall_), out=lerp(10, 45, fall_), bend=lerp(25, 55, fall_))
    leg(p, "R", fwd=lerp(lerp(0, 40, buckle), -4 + 10 * settle, fall_), knee=lerp(lerp(0, 80, buckle), 8 + 22 * settle, fall_), foot=-10 * fall_, out=8 * fall_)
    leg(p, "L", fwd=lerp(lerp(0, 25, buckle), -6, fall_), knee=lerp(lerp(0, 60, buckle), 12, fall_), foot=-10 * fall_, out=14 * fall_)
    return p


def ride(t):
    # Seated on the trail bike / jet ski, hands on the handlebar, absorbing small bumps.
    p = Pose()
    bump = .5 * wave(t) + .5 * wave(t * 2 % 1.0, .2)
    hips(p, lift=.02 + .012 * bump, fwd=-.05, pitch=-2 * bump)
    torso(p, lean=24 + 2 * bump, head=16)
    arm(p, "R", fwd=58, out=24, bend=44 + 3 * bump)
    arm(p, "L", fwd=58, out=24, bend=44 + 3 * bump)
    leg(p, "R", fwd=72, knee=88 + 2 * bump, foot=8, out=14)
    leg(p, "L", fwd=72, knee=88 + 2 * bump, foot=8, out=14)
    return p


def glide(t):
    # Hanging from the GARUDA glider bar: arms up and wide, legs together trailing behind.
    p = Pose()
    s = wave(t)
    hips(p, pitch=14 + 2 * s, roll=3 * s)
    torso(p, lean=4, head=12, side=2 * s)
    arm(p, "R", fwd=165, out=30, bend=14)
    arm(p, "L", fwd=165, out=30, bend=14)
    leg(p, "R", fwd=-12 + 3 * s, knee=18, foot=-35)
    leg(p, "L", fwd=-8 - 3 * s, knee=24, foot=-35)
    return p


# name, function, frames, loop
CLIPS = [
    ("Idle", idle, 180, True),
    ("Run", run, 40, True),
    ("Sprint", sprint, 32, True),
    ("Jump", jump, 60, True),
    ("Fall", fall, 48, True),
    ("Climb", climb, 54, True),
    ("ClimbIdle", climb_idle, 150, True),
    ("Hang", hang, 120, True),
    ("Stab", stab, 60, True),
    ("StabRelease", stab_release, 24, False),
    ("Dodge", dodge, 36, False),
    ("Swim", swim, 72, True),
    ("Death", death, 72, False),
    ("Ride", ride, 60, True),
    ("Glide", glide, 90, True),
]


def build_all(rig, ob=None):
    """Delete existing actions and author every clip on `rig`. Returns [(name, frames)]."""
    global ARM_DOWN
    bones = rig.data.bones
    # Angle the rest arm sits below horizontal (A-pose); hanging = 90 - that, minus a little for the deltoid.
    d = (bones["upper_arm.R"].tail_local - bones["upper_arm.R"].head_local).normalized()
    ARM_DOWN = 90 - math.degrees(math.asin(max(-1, min(1, -d.z)))) - 8
    rest = {b.name: b.matrix_local.to_quaternion() for b in bones}
    rest3 = {b.name: b.matrix_local.to_3x3() for b in bones}

    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    if rig.animation_data is None:
        rig.animation_data_create()
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'

    report = []
    for name, fn, frames, loop in CLIPS:
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        prev = {}
        for f in range(frames + 1):
            t = (f / frames) if loop else min(1.0, f / frames)
            if loop and f == frames: t = 0.0          # exact seam
            pose = fn(t)
            for pb in rig.pose.bones:
                q_arm = pose.get(pb.name, Quaternion())
                B = rest[pb.name]
                q = B.inverted() @ q_arm @ B
                if pb.name in prev and prev[pb.name].dot(q) < 0:
                    q = -q
                prev[pb.name] = q
                pb.rotation_quaternion = q
                pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
                if pb.name == "hips":
                    off = pose.get("_hips_loc", Vector())
                    pb.location = rest3["hips"].inverted() @ off
                    pb.keyframe_insert("location", frame=f, group=pb.name)
        report.append((name, frames))
    bpy.ops.object.mode_set(mode='OBJECT')
    return report
