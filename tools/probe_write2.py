import ctypes, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

vm = ctypes.WinDLL(r"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")
L = ctypes.c_long
vm.VBVMR_Login.restype = L
vm.VBVMR_GetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.POINTER(ctypes.c_float)]
vm.VBVMR_GetParameterFloat.restype = L
vm.VBVMR_SetParameterFloat.argtypes = [ctypes.c_char_p, ctypes.c_float]
vm.VBVMR_SetParameterFloat.restype = L
vm.VBVMR_IsParametersDirty.restype = L
print("login", vm.VBVMR_Login())


def getf(name):
    v = ctypes.c_float(0)
    r = vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    return round(v.value, 3)


def setf(name, val):
    return vm.VBVMR_SetParameterFloat(name.encode(), ctypes.c_float(val))


# ---- 1) 恢复被我测试改动过的两个值 ----
print("restore Bus[0].Gain -> -9.8")
setf("Bus[0].Gain", -9.8)
print("restore Strip[3].Gain -> -3.49")
setf("Strip[3].Gain", -3.49)

# ---- 2) 测量写入生效延迟 ----
def wait_apply(name, target, max_sec=6.0):
    t0 = time.time()
    while time.time() - t0 < max_sec:
        v = getf(name)
        if abs(v - target) < 0.05:
            return round(time.time() - t0, 3), v
        time.sleep(0.05)
    return None, getf(name)


dt, v = wait_apply("Bus[0].Gain", -9.8)
print(f"Bus[0].Gain applied after {dt}s -> {v}")
dt, v = wait_apply("Strip[3].Gain", -3.49)
print(f"Strip[3].Gain applied after {dt}s -> {v}")

# ---- 3) 再测一次，看第二次写入是否更快 ----
setf("Bus[1].Gain", -3.19)
dt, v = wait_apply("Bus[1].Gain", -3.19)
print(f"Bus[1].Gain applied after {dt}s -> {v}")

# ---- 4) 是否 IsParametersDirty 会触发刷新 ----
print("dirty flag:", vm.VBVMR_IsParametersDirty())
setf("Bus[2].Gain", -4.51)
for i in range(20):
    time.sleep(0.1)
    vm.VBVMR_IsParametersDirty()
print("after polling dirty, Bus[2].Gain =", getf("Bus[2].Gain"))

vm.VBVMR_Logout()
print("final values:")
for n in ["Bus[0].Gain", "Bus[1].Gain", "Bus[2].Gain", "Strip[3].Gain", "Strip[4].Gain"]:
    print("  ", n, getf(n))
