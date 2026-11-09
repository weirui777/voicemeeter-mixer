import ctypes, os, sys

dll_dir = r"C:\Program Files (x86)\VB\Voicemeeter"
for name in ("VoicemeeterRemote64.dll",):
    path = os.path.join(dll_dir, name)
    if not os.path.exists(path):
        print("MISSING", path); sys.exit(1)
    vm = ctypes.WinDLL(path)

L = ctypes.c_long
P = ctypes.POINTER

vm.VBVMR_Login.restype = L
vm.VBVMR_Logout.restype = L
vm.VBVMR_GetVoicemeeterType.argtypes = [P(L)]
vm.VBVMR_GetVoicemeeterType.restype = L
vm.VBVMR_GetVoicemeeterVersion.argtypes = [P(L)]
vm.VBVMR_GetVoicemeeterVersion.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, P(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_SetParameterFloat.restype = L
vm.VBVMR_GetParameterStringA.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_GetParameterStringA.restype = L
vm.VBVMR_IsParametersDirty.restype = L

print("Login ->", vm.VBVMR_Login())
t = L(0); vm.VBVMR_GetVoicemeeterType(ctypes.byref(t)); print("Type ->", t.value)
v = L(0); vm.VBVMR_GetVoicemeeterVersion(ctypes.byref(v)); print("Version raw ->", hex(v.value & 0xFFFFFFFF))


def getf(name):
    val = ctypes.c_float(0)
    r = vm.VBVMR_GetParameterFloat(name.encode("ascii"), ctypes.byref(val))
    return r, round(val.value, 3)


def gets(name, n=256):
    buf = ctypes.create_string_buffer(n)
    r = vm.VBVMR_GetParameterStringA(name.encode("ascii"), buf)
    return r, buf.value.decode("utf-8", "replace")


print("\n-- float probe --")
cands = []
for i in range(0, 12):
    cands.append(f"Strip[{i}].Gain")
    cands.append(f"Strip[{i}].Mute")
for i in range(0, 6):
    cands.append(f"Bus[{i}].Gain")
    cands.append(f"Bus[{i}].Mute")
cands += ["Master.Gain", "Master.Mute", "Master[0].Gain", "Bus.Gain", "Strip.Gain",
          "Master.A1", "Strip[0].A1", "Strip[0].Headphone", "Strip[0].Comp",
          "Strip[0].Gate", "Strip[0].Device", "Strip[0].Physical", "Strip[0].Phys"]
for c in cands:
    r, val = getf(c)
    if r == 0:
        print(f"  OK   {c:24s} = {val}")
    else:
        print(f"  err{r:<4d} {c}")

print("\n-- string probe --")
scands = []
for i in range(0, 12):
    scands.append(f"Strip[{i}].Name")
    scands.append(f"Strip[{i}].Label")
    scands.append(f"Strip[{i}].Device")
for i in range(0, 6):
    scands.append(f"Bus[{i}].Name")
    scands.append(f"Bus[{i}].Device")
scands += ["Master.Name", "Bus.Name"]
for c in scands:
    r, s = gets(c)
    if r == 0:
        print(f"  OK   {c:22s} = {s!r}")
    else:
        print(f"  err{r:<4d} {c}")

vm.VBVMR_Logout()
print("\nDone")
