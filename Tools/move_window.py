import ctypes
import tkinter as tk
from ctypes import wintypes


user32 = ctypes.windll.user32


EnumWindowsProc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

SW_RESTORE = 9
SWP_NOZORDER = 0x0004
SWP_NOACTIVATE = 0x0010

SM_XVIRTUALSCREEN = 76
SM_YVIRTUALSCREEN = 77
SM_CXVIRTUALSCREEN = 78
SM_CYVIRTUALSCREEN = 79


def make_dpi_aware():
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        try:
            user32.SetProcessDPIAware()
        except Exception:
            pass


def get_window_text(hwnd):
    length = user32.GetWindowTextLengthW(hwnd)
    if length <= 0:
        return ""
    buf = ctypes.create_unicode_buffer(length + 1)
    user32.GetWindowTextW(hwnd, buf, length + 1)
    return buf.value


def find_touhou_windows():
    windows = []

    @EnumWindowsProc
    def callback(hwnd, _):
        if not user32.IsWindowVisible(hwnd):
            return True

        title = get_window_text(hwnd)
        if "TOUHOU" in title.upper():
            windows.append((hwnd, title))
        return True

    user32.EnumWindows(callback, 0)
    return windows


def choose_window(windows):
    print("找到以下包含 TOUHOU 的窗口：")
    for i, (hwnd, title) in enumerate(windows, 1):
        print(f"{i}. hwnd=0x{hwnd:08X}  title={title}")

    while True:
        choice = input("请选择窗口编号：").strip()
        if choice.isdigit() and 1 <= int(choice) <= len(windows):
            return windows[int(choice) - 1]
        print("输入无效，请重新输入。")


def select_rectangle():
    vx = user32.GetSystemMetrics(SM_XVIRTUALSCREEN)
    vy = user32.GetSystemMetrics(SM_YVIRTUALSCREEN)
    vw = user32.GetSystemMetrics(SM_CXVIRTUALSCREEN)
    vh = user32.GetSystemMetrics(SM_CYVIRTUALSCREEN)

    root = tk.Tk()
    root.title("框选目标位置")
    root.overrideredirect(True)
    root.attributes("-topmost", True)
    root.attributes("-alpha", 0.25)
    root.geometry(f"{vw}x{vh}+{vx}+{vy}")
    root.configure(bg="black")

    canvas = tk.Canvas(root, bg="black", highlightthickness=0, cursor="crosshair")
    canvas.pack(fill="both", expand=True)

    state = {"start": None, "rect": None, "result": None}

    def on_down(event):
        state["start"] = (event.x_root, event.y_root)
        if state["rect"] is not None:
            canvas.delete(state["rect"])
        state["rect"] = canvas.create_rectangle(
            event.x,
            event.y,
            event.x,
            event.y,
            outline="red",
            width=3,
        )

    def on_drag(event):
        if state["start"] is None or state["rect"] is None:
            return
        sx, sy = state["start"]
        canvas.coords(state["rect"], sx - vx, sy - vy, event.x_root - vx, event.y_root - vy)

    def on_up(event):
        if state["start"] is None:
            return

        sx, sy = state["start"]
        ex, ey = event.x_root, event.y_root
        left, right = sorted((sx, ex))
        top, bottom = sorted((sy, ey))

        if right - left < 10 or bottom - top < 10:
            print("矩形太小，已取消。")
            state["result"] = None
        else:
            state["result"] = (left, top, right - left, bottom - top)

        root.quit()

    def on_escape(_):
        state["result"] = None
        root.quit()

    root.bind("<ButtonPress-1>", on_down)
    root.bind("<B1-Motion>", on_drag)
    root.bind("<ButtonRelease-1>", on_up)
    root.bind("<Escape>", on_escape)

    print("请在屏幕上拖拽框选目标矩形，按 Esc 取消。")
    root.mainloop()
    root.destroy()
    return state["result"]


def move_window(hwnd, rect):
    x, y, width, height = rect
    user32.ShowWindow(hwnd, SW_RESTORE)
    ok = user32.SetWindowPos(hwnd, None, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE)
    if not ok:
        raise ctypes.WinError(ctypes.get_last_error())


def main():
    make_dpi_aware()

    windows = find_touhou_windows()
    if not windows:
        print("没有找到标题包含 TOUHOU 的窗口。")
        return

    hwnd, title = choose_window(windows)
    print(f"已选择：hwnd=0x{hwnd:08X} title={title}")

    rect = select_rectangle()
    if rect is None:
        print("已取消。")
        return

    print(f"移动窗口到：x={rect[0]} y={rect[1]} width={rect[2]} height={rect[3]}")
    move_window(hwnd, rect)
    print("完成。")


if __name__ == "__main__":
    main()
