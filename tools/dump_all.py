import ctypes, sys, time

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterStringA.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

def labels():
    out = {}
    for i in range(8):
        for pfx in ("Strip", "Bus"):
            b = ctypes.create_string_buffer(128)
            vm.VBVMR_GetParameterStringA(("%s[%d].Label" % (pfx, i)).encode(), b)
            out["%s[%d]" % (pfx, i)] = b.value.decode('latin-1')
    return out

def read():
    d = {}
    for i in range(8):
        for pfx in ("Strip", "Bus"):
            for prm in ("Gain", "Mute"):
                v = ctypes.c_float()
                ok = vm.VBVMR_GetParameterFloat(("%s[%d].%s" % (pfx, i, prm)).encode(), ctypes.byref(v))
                d["%s[%d].%s" % (pfx, i, prm)] = (round(v.value, 2) if ok == 0 else None)
    return d

print("login", vm.VBVMR_Login())
lab = labels()
first = read()
for k in sorted(first, key=lambda s: (s[0], int(s.split('[')[1].split(']')[0]))):
    if k.endswith(".Gain"):
        base = k[:-5]
        print("%-9s %-16s gain=%8s mute=%s" % (base, lab.get(base, '?'), first[k], first[base + ".Mute"]))

if len(sys.argv) > 1 and sys.argv[1] == "watch":
    for r in range(int(sys.argv[2]) if len(sys.argv) > 2 else 10):
        time.sleep(1)
        now = read()
        diff = {k: (first[k], now[k]) for k in now if now[k] != first[k]}
        print("[t+%ds] %s" % (r + 1, diff if diff else "no change"))
        first = now
