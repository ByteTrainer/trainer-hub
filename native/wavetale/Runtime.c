#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <bcrypt.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <limits.h>
#include "MinHook.h"

// Layout and method addresses for Steam build 9957664.
enum {
    Health = 1, Glide = 2, LevelGlide = 4, Jump = 8,
    FreezeTimer = 32, Speed = 64, All = 111
};
enum {
    PlayerState = 0x78, CombatDefinition = 0x60, HealthDefinition = 0x28,
    MainState = 0x10, Persistent = 0x18, Momentary = 0x20,
    VerticalSpeed = 0x28,
    JumpData = 0xc0, GlideData = 0x140, HealthData = 0x168, Gliding = 0x1c7,
    IsAlive = 0x10, CurrentHealth = 0x14, MaxHealth = 0x10,
    GlideState = 0x10, GlideStartTime = 0x14, TotalGlideTime = 0x20, RepeatGlideTime = 0x28,
    CanDoubleJump = 0x48, DoubleJumpState = 0x50, JumpPressed = 0x5c,
    RestrictMove = 0xad, RestrictJump = 0xac, InputBlocked = 0xbd,
    NeedsSave = 0x19, CollectedSparks = 0x40, SpentSparks = 0x44
};
typedef const void *Method;
typedef struct { float x, y, z; } Vector3;
typedef void (*Action)(void *, Method);
typedef void *(*Singleton)(Method);
typedef void (*PlayerUpdate)(void *, void *, Method);
typedef void (*HealthUpdate)(void *, int, bool, Method);
typedef int (*Move)(void *, const Vector3 *, int, bool, Method);

static PlayerUpdate originalUpdate, originalDamage;
static Action originalGlide, originalJump, originalMovement, originalTimers;
static void (*originalGravity)(void *, bool, Method);
static HealthUpdate updateHealth;
static Singleton sparkManager;
static void (*updateSparks)(bool, Method);
static Move originalMove;
static Action updateTimerText;
static float (*gameTime)(Method);
static uint8_t *assemblyBase;
static wchar_t folder[MAX_PATH];
static DWORD pid;
static uint64_t lease, nextPoll;
static unsigned flags;
static float speed;
static char session[33], ack[33], error[96];
static int applied;
static _Thread_local void *movingPlayer;

#define FIELD(type, object, offset) (*(type *)((uint8_t *)(object) + (offset)))
static void *field(void *object, size_t offset) { return object ? FIELD(void *, object, offset) : NULL; }
static void *persistent(void *player) { return field(field(player, PlayerState), Persistent); }

static uint64_t utcTicks(void) {
    FILETIME time;
    GetSystemTimeAsFileTime(&time);
    return (((uint64_t)time.dwHighDateTime << 32) | time.dwLowDateTime) + 504911232000000000ULL;
}

static bool enabled(unsigned option) { return (flags & option) && lease > utcTicks(); }
static bool alive(void *player) {
    void *health = field(persistent(player), HealthData);
    return health && FIELD(bool, health, IsAlive) && FIELD(int, health, CurrentHealth) > 0;
}

static void takeDamage(void *player, void *dealer, Method method) {
    if (!(enabled(Health) && alive(player))) originalDamage(player, dealer, method);
}

static void glide(void *player, Method method) {
    void *data = field(persistent(player), GlideData);
    if (data && enabled(Glide) && FIELD(int, data, GlideState)) {
        FIELD(float, data, GlideStartTime) = gameTime(NULL);
        FIELD(float, data, TotalGlideTime) = 0;
        FIELD(float, data, RepeatGlideTime) = 0;
    }
    originalGlide(player, method);
}

static void gravity(void *player, bool grounded, Method method) {
    originalGravity(player, grounded, method);
    void *data = persistent(player);
    if (data && FIELD(bool, data, Gliding) && enabled(LevelGlide))
        FIELD(float, data, VerticalSpeed) = 0;
}

static void jump(void *player, Method method) {
    void *state = field(player, PlayerState);
    void *data = field(state, Persistent);
    void *input = field(state, Momentary);
    void *jumpData = field(data, JumpData);
    if (input && jumpData && enabled(Jump) && FIELD(bool, input, JumpPressed) &&
        !FIELD(bool, input, RestrictJump) && !FIELD(bool, input, InputBlocked) &&
        FIELD(int, jumpData, DoubleJumpState) == 3) {
        FIELD(bool, jumpData, CanDoubleJump) = true;
        FIELD(int, jumpData, DoubleJumpState) = 2;
    }
    originalJump(player, method);
}

static void movement(void *player, Method method) {
    void *previous = movingPlayer;
    movingPlayer = player;
    originalMovement(player, method);
    movingPlayer = previous;
}

static int move(void *player, const Vector3 *displacement, int mode, bool stuck, Method method) {
    void *state = field(player, PlayerState);
    void *input = field(state, Momentary);
    if (player != movingPlayer || !enabled(Speed) || mode != 15 || !input ||
        FIELD(int, state, MainState) != 0 || FIELD(bool, input, RestrictMove) || FIELD(bool, input, InputBlocked))
        return originalMove(player, displacement, mode, stuck, method);

    Vector3 scaled = *displacement;
    scaled.x *= speed;
    scaled.z *= speed;
    // Keep the game's normal collision checks, without feeding the scaled displacement back into velocity.
    return originalMove(player, &scaled, mode & ~8, stuck, method);
}

static void timers(void *ui, Method method) {
    if (enabled(FreezeTimer)) updateTimerText(ui, NULL);
    else originalTimers(ui, method);
}

static bool readText(const wchar_t *name, char *buffer, DWORD capacity) {
    wchar_t path[MAX_PATH];
    swprintf(path, MAX_PATH, L"%ls\\%ls", folder, name);
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                             NULL, OPEN_EXISTING, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return false;
    DWORD length = 0;
    bool ok = ReadFile(file, buffer, capacity - 1, &length, NULL);
    CloseHandle(file);
    if (!ok || length == capacity - 1) return false;
    buffer[length] = 0;
    return true;
}

static bool validLease(unsigned owner, uint64_t expiry, uint64_t now) {
    return owner == pid && expiry > now && expiry - now <= 50000000ULL;
}

static void readSettings(uint64_t now) {
    char buffer[256], id[33], extra;
    unsigned owner, mask;
    unsigned long long expiry;
    float value;
    flags = 0;
    lease = 0;
    if (!readText(L"settings.txt", buffer, sizeof(buffer)) ||
        sscanf(buffer, "%u|%llu|%u|%32[0-9a-f]|1,1,1,1,1,1,%f %c", &owner, &expiry, &mask, id, &value, &extra) != 5 ||
        !validLease(owner, expiry, now) || (mask & ~All) || strlen(id) != 32 || !(value >= 1 && value <= 3)) return;

    if (strcmp(session, id)) {
        strcpy(session, id);
        ack[0] = error[0] = 0;
        applied = 0;
    }
    flags = mask;
    lease = expiry;
    speed = value;
}

static void setSparks(void *manager, uint64_t now) {
    char buffer[256], id[33], nonce[33], extra;
    unsigned owner, choice;
    unsigned long long expiry, amount;
    if (!manager || !readText(L"resource.txt", buffer, sizeof(buffer)) ||
        sscanf(buffer, "%u|%llu|%32[0-9a-f]|%32[0-9a-f]|%u|%llu %c",
               &owner, &expiry, id, nonce, &choice, &amount, &extra) != 6 ||
        !validLease(owner, expiry, now) || strcmp(id, session) || strlen(nonce) != 32 ||
        !strcmp(nonce, ack) || choice != 1) return;

    strcpy(ack, nonce);
    error[0] = 0;
    int spent = FIELD(int, manager, SpentSparks);
    int collected = FIELD(int, manager, CollectedSparks);
    applied = collected >= spent && spent >= 0 ? collected - spent : 0;
    if (spent < 0 || amount > 1000000000ULL || amount + (unsigned)spent > INT_MAX) {
        strcpy(error, "The requested balance exceeds the game limit.");
        return;
    }
    FIELD(int, manager, CollectedSparks) = spent + (int)amount;
    FIELD(bool, manager, NeedsSave) = true;
    applied = (int)amount;
    updateSparks(true, NULL);
}

static void publish(uint64_t now, bool ready) {
    char buffer[256];
    int length = snprintf(buffer, sizeof(buffer), "2|%lu|%llu|%d|%u|%s|%s|%d|%s",
                          pid, (unsigned long long)now, ready, ready ? flags : 0, session, ack, applied, error);
    wchar_t pending[MAX_PATH], status[MAX_PATH];
    swprintf(pending, MAX_PATH, L"%ls\\status-%lu.tmp", folder, pid);
    swprintf(status, MAX_PATH, L"%ls\\status.txt", folder);
    HANDLE file = CreateFileW(pending, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return;
    DWORD written = 0;
    bool ok = WriteFile(file, buffer, (DWORD)length, &written, NULL) && written == (DWORD)length;
    CloseHandle(file);
    if (ok) MoveFileExW(pending, status, MOVEFILE_REPLACE_EXISTING);
}

static void update(void *player, void *input, Method method) {
    uint64_t now = utcTicks();
    if (now >= nextPoll) {
        nextPoll = now + 1000000;
        bool ready = alive(player);
        if (ready) {
            readSettings(now);
            if (lease) setSparks(sparkManager(NULL), now);
            if (enabled(Health)) {
                void *health = field(persistent(player), HealthData);
                void *definition = field(field(player, CombatDefinition), HealthDefinition);
                if (definition && FIELD(int, health, CurrentHealth) < FIELD(int, definition, MaxHealth))
                    updateHealth(player, FIELD(int, definition, MaxHealth), true, NULL);
            }
        } else flags = 0;
        publish(now, ready);
    }
    originalUpdate(player, input, method);
}

static bool verifyAssembly(HMODULE assembly) {
    static const uint8_t expected[32] = {
        0xb7, 0xc3, 0xdc, 0x9d, 0xd9, 0x7b, 0xe6, 0x54, 0xde, 0x48, 0x4c, 0x79, 0x96, 0xea, 0xc9, 0xb0,
        0xfe, 0x58, 0x86, 0x8c, 0xa5, 0x1c, 0x19, 0xe5, 0x02, 0xfc, 0xb7, 0x3d, 0xba, 0xc7, 0x0f, 0x7b
    };
    wchar_t path[MAX_PATH];
    DWORD length = GetModuleFileNameW(assembly, path, MAX_PATH);
    if (!length || length >= MAX_PATH) return false;
    HANDLE file = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    if (file == INVALID_HANDLE_VALUE) return false;
    BCRYPT_ALG_HANDLE algorithm = NULL;
    BCRYPT_HASH_HANDLE hash = NULL;
    uint8_t buffer[16384], digest[32];
    DWORD bytes;
    bool ok = false;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, NULL, 0) < 0 ||
        BCryptCreateHash(algorithm, &hash, NULL, 0, NULL, 0, 0) < 0) goto cleanup;
    do {
        if (!ReadFile(file, buffer, sizeof(buffer), &bytes, NULL) ||
            (bytes && BCryptHashData(hash, buffer, bytes, 0) < 0)) goto cleanup;
    } while (bytes);
    ok = BCryptFinishHash(hash, digest, sizeof(digest), 0) >= 0 && memcmp(digest, expected, sizeof(expected)) == 0;
cleanup:
    if (hash) BCryptDestroyHash(hash);
    if (algorithm) BCryptCloseAlgorithmProvider(algorithm, 0);
    CloseHandle(file);
    return ok;
}


static bool installHooks(HMODULE assembly) {
    assemblyBase = (uint8_t *)assembly;
    updateTimerText = (Action)(assemblyBase + 0x3ecc40);
    updateHealth = (HealthUpdate)(assemblyBase + 0x300720);
    gameTime = (float (*)(Method))(assemblyBase + 0x15896c0);
    sparkManager = (Singleton)(assemblyBase + 0x6e1fd0);
    updateSparks = (void (*)(bool, Method))(assemblyBase + 0x3eb750);
    struct Hook { void *target, *detour, **original; };
    const struct Hook hooks[] = {
        {assemblyBase + 0x346dc0, update, (void **)&originalUpdate},
        {assemblyBase + 0x3008f0, takeDamage, (void **)&originalDamage},
        {assemblyBase + 0x31d6e0, glide, (void **)&originalGlide},
        {assemblyBase + 0x31ea00, gravity, (void **)&originalGravity},
        {assemblyBase + 0x318060, jump, (void **)&originalJump},
        {assemblyBase + 0x303970, movement, (void **)&originalMovement},
        {assemblyBase + 0x338430, move, (void **)&originalMove},
        {assemblyBase + 0x3eca40, timers, (void **)&originalTimers}
    };
    if (MH_Initialize() != MH_OK) return false;
    for (size_t i = 0; i < sizeof(hooks) / sizeof(hooks[0]); ++i) {
        if (MH_CreateHook(hooks[i].target, hooks[i].detour, hooks[i].original) != MH_OK) {
            MH_Uninitialize();
            return false;
        }
    }
    if (MH_EnableHook(MH_ALL_HOOKS) == MH_OK) return true;
    MH_Uninitialize();
    return false;
}

static DWORD WINAPI run(void *unused) {
    (void)unused;
    HMODULE runtime;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                           (LPCWSTR)&run, &runtime)) return 0;
    HMODULE assembly = GetModuleHandleW(L"GameAssembly.dll");
    if (!assembly || !verifyAssembly(assembly)) return 0;
    DWORD length = GetTempPathW(MAX_PATH, folder);
    if (!length || length >= MAX_PATH - 64) return 0;
    wcscat(folder, L"TrainerHub-1823930");
    CreateDirectoryW(folder, NULL);
    pid = GetCurrentProcessId();
    installHooks(assembly);
    return 0;
}

__declspec(dllexport) void ByteTrainerRuntime(void) {}

BOOL WINAPI DllMain(HINSTANCE module, DWORD reason, LPVOID reserved) {
    (void)module;
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH) {
        HANDLE thread = CreateThread(NULL, 0, run, NULL, 0, NULL);
        if (thread) CloseHandle(thread);
    }
    return TRUE;
}
