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
    vm.VBVMR_GetParameterFloat(name.encode(), ctypes.byref(v))
    return round(v.value, 3)


def setf(name, val):
    return vm.VBVMR_SetParameterFloat(name.encode(), ctypes.c_float(val))


def pump_and_wait(name, target, max_sec=5.0):
    """调用 IsParametersDirty 让 VM 处理待写入值"""
    t0 = time.time()
    while time.time() - t0 < max_sec:
        vm.VBVMR_IsParametersDirty()
        v = getf(name)
        if abs(v - target) < 0.05:
            return round(time.time() - t0, 3), v
        time.sleep(0.01)
    return None, getf(name)


def set_and_wait(name, target):
    setf(name, target)
    return pump_and_wait(name, target)


print("\n[实验 1] 写入 + 定期泵 IsParametersDirty")
dt, v = set_and_wait("Bus[1].Gain", -10.0)
print(f"  Bus[1].Gain -> -10.0  生效耗时 {dt}s  当前 {v}")

print("\n[实验 2] 只写不泵（对照组，3 秒）")
setf("Bus[1].Gain", -7.0)
time.sleep(3.0)
print("  当前值 =", getf("Bus[1].Gain"), "（预期仍是 -10.0，说明未生效）")

print("\n[实验 3] 再泵一次，看是否补上")
dt, v = pump_and_wait("Bus[1].Gain", -7.0, 5.0)
print(f"  Bus[1].Gain -> -7.0  生效耗时 {dt}s  当前 {v}")

print("\n[恢复] Bus[1].Gain -> -3.19")
dt, v = set_and_wait("Bus[1].Gain", -3.19)
print(f"  生效耗时 {dt}s  当前 {v}")

print("\n[恢复] 其它通道核对")
for name, val in (("Bus[0].Gain", -9.8), ("Bus[2].Gain", -4.51), ("Strip[3].Gain", -3.49), ("Strip[4].Gain", -2.32)):
    dt, v = set_and_wait(name, val)
    print(f"  {name} -> {val}: {dt}s, 现值 {v}")

vm.VBVMR_Logout()
