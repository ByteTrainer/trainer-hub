#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <math.h>

typedef struct { float x, y, z; } Position;
typedef struct { Position delta; float distance2; } Target;
static HWND tracker;
static Target displayTargets[2];
static unsigned displayFlags;
static double displayYaw;

static LRESULT CALLBACK paintTracker(HWND window, UINT message, WPARAM w, LPARAM l)
{
    if (message != WM_PAINT) return DefWindowProcW(window, message, w, l);
    PAINTSTRUCT paint;
    HDC dc = BeginPaint(window, &paint);
    RECT bounds;
    GetClientRect(window, &bounds);
    HBRUSH background = CreateSolidBrush(RGB(20, 29, 23));
    FillRect(dc, &bounds, background);
    DeleteObject(background);
    SetBkMode(dc, TRANSPARENT);
    SelectObject(dc, GetStockObject(DEFAULT_GUI_FONT));
    int y = 0;
    for (int i = 0; i < 2; ++i) {
        if (!(displayFlags & (2048u << i))) continue;
        const Target target = displayTargets[i];
        const wchar_t *label = i ? L"Time Rift" : L"Colecion\x00e1vel";
        wchar_t hint[100];
        COLORREF color = i ? RGB(155, 206, 255) : RGB(194, 243, 111);
        SetTextColor(dc, color);
        if (isfinite(target.distance2)) {
            const double angle = atan2(target.delta.y, target.delta.x) - displayYaw;
            const double x = sin(angle), forward = cos(angle);
            POINT points[] = {
                {23 + (LONG)lround(14*x), y + 21 - (LONG)lround(14*forward)},
                {23 + (LONG)lround(-8*x + 6*forward), y + 21 + (LONG)lround(8*forward + 6*x)},
                {23 + (LONG)lround(-8*x - 6*forward), y + 21 + (LONG)lround(8*forward - 6*x)}
            };
            HBRUSH arrow = CreateSolidBrush(color);
            HGDIOBJ previous = SelectObject(dc, arrow);
            Polygon(dc, points, 3);
            SelectObject(dc, previous);
            DeleteObject(arrow);
            const wchar_t *height = target.delta.z > 200 ? L"  /  acima" :
                target.delta.z < -200 ? L"  /  abaixo" : L"";
            swprintf(hint, 100, L"%ls  /  %.0f m%ls", label, sqrtf(target.distance2)/100, height);
        } else {
            swprintf(hint, 100, L"%ls  /  sem alvo na fase", label);
        }
        TextOutW(dc, 44, y + 13, hint, (int)wcslen(hint));
        y += 42;
    }
    EndPaint(window, &paint);
    return 0;
}

static void showTracker(DWORD pid, unsigned flags, const Target targets[2], int yaw)
{
    if (!flags && !tracker) return;
    MSG message;
    while (PeekMessageW(&message, NULL, 0, 0, PM_REMOVE)) DispatchMessageW(&message);
    const HWND game = GetForegroundWindow();
    DWORD windowPid = 0;
    RECT area;
    POINT origin = {0};
    GetWindowThreadProcessId(game, &windowPid);
    if (!flags || windowPid != pid || IsIconic(game) ||
        !GetClientRect(game, &area) || !ClientToScreen(game, &origin)) {
        if (tracker && IsWindowVisible(tracker)) ShowWindow(tracker, SW_HIDE);
        return;
    }
    if (!tracker) {
        WNDCLASSW cls = {0};
        cls.lpfnWndProc = paintTracker;
        cls.hInstance = GetModuleHandleW(NULL);
        cls.lpszClassName = L"ByteTrainerHatTargetOverlay";
        RegisterClassW(&cls);
        tracker = CreateWindowExW(WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE |
            WS_EX_LAYERED | WS_EX_TRANSPARENT, cls.lpszClassName, NULL, WS_POPUP,
            0, 0, 264, 84, NULL, NULL, cls.hInstance, NULL);
        if (!tracker) return;
        SetLayeredWindowAttributes(tracker, 0, 235, LWA_ALPHA);
    }
    displayTargets[0] = targets[0];
    displayTargets[1] = targets[1];
    displayFlags = flags;
    displayYaw = ((unsigned)yaw & 65535) * (6.283185307179586 / 65536);
    SetWindowPos(tracker, HWND_TOPMOST, origin.x + (area.right - 264)/2, origin.y + 22,
        264, flags == 6144 ? 84 : 42, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    InvalidateRect(tracker, NULL, FALSE);
    UpdateWindow(tracker);
}
