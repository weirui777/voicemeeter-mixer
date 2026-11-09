import ctypes, sys, io, time, subprocess
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

user32 = ctypes.windll.user32
vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
L = ctypes.c_long
vm.VBVMR_Login.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_SetParameterFloat.restype = L
print("login", vm.VBVMR_Login())


def getf(name):
    v = ctypes.c_float(0)
    vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    return round(v.value, 3)


def setf(name, val):
    return vm.VBVMR_SetParameterFloat(name.encode(), ctypes.c_float(val))


def wait_apply(name, target, max_sec=20.0):
    t0 = time.time()
    while time.time() - t0 < max_sec:
        v = getf(name)
        if abs(v - target) < 0.05:
            return round(time.time() - t0, 2), v
        time.sleep(0.05)
    return None, getf(name)


def focus_vm():
    out = subprocess.run(
        ["powershell", "-NoProfile", "-Command",
         "(Get-Process voicemeeterpro_x64).MainWindowHandle"],
        capture_output=True, text=True)
    try:
        h = int(out.stdout.strip())
    except Exception:
        print("no vm window")
        return
    user32.SetForegroundWindow(h)
    print("foreground ->", h)


print("\n--- Phase A: VM in background ---")
setf("Bus[1].Gain", -8.0)
dt, v = wait_apply("Bus[1].Gain", -8.0, 12.0)
print(f"Bus[1].Gain -> -8.0 applied after {dt}s (value={v})")

print("\n--- Phase B: bring VM to foreground ---")
focus_vm()
time.sleep(1.0)
setf("Bus[1].Gain", -3.19)
dt, v = wait_apply("Bus[1].Gain", -3.19, 12.0)
print(f"Bus[1].Gain -> -3.19 applied after {dt}s (value={v})")
setf("Bus[2].Gain", -6.0)
dt, v = wait_apply("Bus[2].Gain", -6.0, 12.0)
print(f"Bus[2].Gain -> -6.0 applied after {dt}s (value={v})")

print("\n--- restore all values I touched ---")
for name, val in (("Bus[0].Gain", -9.8), ("Bus[1].Gain", -3.19), ("Bus[2].Gain", -4.51),
                  ("Strip[3].Gain", -3.49), ("Strip[4].Gain", -2.32)):
    setf(name, val)
    dt, v = wait_apply(name, val, 15.0)
    print(f"  {name} -> {val}: applied after {dt}s (value={v})")

print("\nfinal read:")
for n in ["Bus[0].Gain", "Bus[1].Gain", "Bus[2].Gain", "Strip[3].Gain", "Strip[4].Gain",
          "Bus[3].Gain", "Bus[4].Gain"]:
    print("  ", n, getf(n))
