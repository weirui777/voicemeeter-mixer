import ctypes, time, sys

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
vm.VBVMR_Login.restype = ctypes.c_long
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_IsParametersDirty.restype = ctypes.c_long

ORIG = {
    "Bus[0].Gain": -9.8,
    "Bus[1].Gain": -3.19,
    "Bus[2].Gain": -4.51,
    "Bus[3].Gain": -60.0,
    "Bus[4].Gain": -60.0,
    "Strip[0].Gain": -60.0,
    "Strip[1].Gain": -60.0,
    "Strip[2].Gain": -60.0,
    "Strip[3].Gain": -3.49,
    "Strip[4].Gain": -2.32,
}


def getf(name):
    v = ctypes.c_float()
    rc = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    return (v.value if rc == 0 else None)


def setf(name, val):
    return vm.VBVMR_SetParameterFloat(name.encode(), ctypes.c_float(val))


print("login", vm.VBVMR_Login())
for name, val in ORIG.items():
    before = getf(name)
    setf(name, val)

deadline = time.time() + 12
pending = dict(ORIG)
while time.time() < deadline and pending:
    vm.VBVMR_IsParametersDirty()
    time.sleep(0.05)
    for name, val in list(pending.items()):
        cur = getf(name)
        if cur is not None and abs(cur - val) < 0.02:
            del pending[name]
        else:
            pending[name] = val
    # keep re-asserting
    for name, val in pending.items():
        setf(name, val)

print("\n--- 结果 ---")
ok = True
for name, val in ORIG.items():
    cur = getf(name)
    good = cur is not None and abs(cur - val) < 0.05
    ok = ok and good
    print("%-14s 目标 %8.2f  现在 %8s  %s" % (name, val, cur, "OK" if good else "!!"))
print("\n全部恢复" if ok else "\n仍有偏差")
vm.VBVMR_Logout()
