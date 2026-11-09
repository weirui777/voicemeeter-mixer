import ctypes, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
L = ctypes.c_long
vm.VBVMR_Login.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
vm.VBVMR_GetParameterStringA.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_GetParameterStringA.restype = L
vm.VBVMR_GetVoicemeeterType.argtypes = [ctypes.POINTER(L)]
print("login", vm.VBVMR_Login())
t = L(0); vm.VBVMR_GetVoicemeeterType(ctypes.byref(t)); print("type", t.value)


def getf(name):
    val = ctypes.c_float(0)
    r = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(val))
    return r, round(val.value, 3)


def gets(name, n=256):
    buf = ctypes.create_string_buffer(n)
    r = vm.VBVMR_GetParameterStringA(name.encode(), buf)
    return r, buf.value.decode("utf-8", "replace")


cands = []
for stem in ["Master", "MASTER", "Master1", "master", "Main", "Output", "Outputs",
             "Global", "VM", "M", "MasterStrip", "MasterVolume", "A0", "Sum"]:
    for prop in ["Gain", "gain", "Volume", "Mute", "Level"]:
        cands.append(f"{stem}.{prop}")
cands += ["Master[0].Gain", "Master[1].Gain", "Bus[8].Gain", "Bus[9].Gain",
          "Strip[8].Gain", "Strip[9].Gain", "A.Gain", "A0.Gain", "B.Gain",
          "Bus[0].Master", "MasterStrip.Gain", "System.Gain", "VM.Gain"]

print("\n== master candidates ==")
for c in cands:
    r, v = getf(c)
    if r == 0:
        print(f"  FOUND {c:22s} = {v}")

print("\n== bus string params (labels/names) ==")
for i in range(0, 8):
    out = []
    for p in ["Label", "Name", "Title"]:
        r, s = gets(f"Bus[{i}].{p}")
        if r == 0:
            out.append(f"{p}={s!r}")
    r, v = getf(f"Bus[{i}].Gain")
    out.append(f"Gain={v}")
    print(f"  Bus[{i}]: " + ", ".join(out))

print("\n== device descriptions (input/output) ==")
for fn in ["VBVMR_Input_GetDeviceNumber", "VBVMR_Output_GetDeviceNumber"]:
    try:
        f = getattr(vm, fn); f.restype = L
        n = f()
        print(f"  {fn} -> {n}")
    except Exception as e:
        print("  err", fn, e)

vm.VBVMR_Input_GetDeviceDescA.argtypes = [L, ctypes.POINTER(L), ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_Input_GetDeviceDescA.restype = L
vm.VBVMR_Output_GetDeviceDescA.argtypes = [L, ctypes.POINTER(L), ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_Output_GetDeviceDescA.restype = L
for label, fn in (("IN", vm.VBVMR_Input_GetDeviceDescA), ("OUT", vm.VBVMR_Output_GetDeviceDescA)):
    for i in range(0, 8):
        ty = L(0); name = ctypes.create_string_buffer(256); hid = ctypes.create_string_buffer(256)
        r = fn(i, ctypes.byref(ty), name, hid)
        if r != 0:
            break
        print(f"  {label}[{i}] type={ty.value} name={name.value.decode('utf-8','replace')!r}")

vm.VBVMR_Logout()
