import ctypes, os

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
L = ctypes.c_long
vm.VBVMR_Login.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
vm.VBVMR_GetParameterStringA.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_GetParameterStringA.restype = L
vm.VBVMR_GetLevel.argtypes = [L, L, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetLevel.restype = L
vm.VBVMR_GetVoicemeeterType.argtypes = [ctypes.POINTER(L)]
print("login", vm.VBVMR_Login())
t = L(0); vm.VBVMR_GetVoicemeeterType(ctypes.byref(t)); print("type", t.value)


def getf(name):
    val = ctypes.c_float(0)
    r = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(val))
    return r, round(val.value, 3)


def gets(name, n=512):
    buf = ctypes.create_string_buffer(n)
    r = vm.VBVMR_GetParameterStringA(name.encode(), buf)
    return r, buf.value.decode("utf-8", "replace")


print("\n== bus range ==")
for i in range(0, 14):
    r, v = getf(f"Bus[{i}].Gain")
    print(f"  Bus[{i}].Gain -> rc={r} val={v}")

print("\n== strip suffix probe ==")
sfx = ["Gain", "Mute", "Solo", "Mono", "Comp", "Gate", "Label", "Denoiser", "Int",
       "A1", "A2", "A3", "B1", "B2", "B3", "Headphone", "Phys", "App", "AppName",
       "Device", "DeviceName", "Eq", "Fade", "Pitch", "Frequency"]
for i in [0, 3, 5, 6, 7]:
    ok = []
    for s in sfx:
        r, v = getf(f"Strip[{i}].{s}")
        if r == 0:
            ok.append(f"{s}={v}")
    r, lab = gets(f"Strip[{i}].Label")
    print(f"  Strip[{i}]: label={lab!r} | " + ", ".join(ok))

print("\n== bus suffix probe ==")
for i in [0, 1, 5, 8]:
    ok = []
    for s in ["Gain", "Mute", "Solo", "Mono", "Comp", "Label", "A1", "A2", "A3", "B1", "B2", "B3", "Headphone", "Eq"]:
        r, v = getf(f"Bus[{i}].{s}")
        if r == 0:
            ok.append(f"{s}={v}")
    r, lab = gets(f"Bus[{i}].Label")
    print(f"  Bus[{i}]: label={lab!r} | " + ", ".join(ok))

print("\n== levels (mode1 strip post-fader / mode3 bus out) ==")
for i in range(0, 10):
    buf = (ctypes.c_float * 8)()
    r = vm.VBVMR_GetLevel(1, i, buf)
    vals = [round(x, 3) for x in buf[:4]]
    print(f"  strip[{i}] mode1 rc={r} {vals}")
for i in range(0, 10):
    buf = (ctypes.c_float * 8)()
    r = vm.VBVMR_GetLevel(3, i, buf)
    vals = [round(x, 3) for x in buf[:4]]
    print(f"  bus[{i}]   mode3 rc={r} {vals}")

print("\n== app listing ==")
for nm in ["Strip[0].App", "Strip[5].App", "Strip[3].App", "Bus[0].App"]:
    r, s = gets(nm)
    print(f"  {nm}: rc={r} {s!r}")

vm.VBVMR_Logout()
