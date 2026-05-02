#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdio.h>
#include <time.h>

#pragma comment(lib, "ws2_32.lib")

// --- Global State ---
HMODULE g_hModule = NULL;
BYTE orig_connect[5];
BYTE orig_WSAConnect[5];
void* addr_connect = NULL;
void* addr_WSAConnect = NULL;

DWORD last_target_ip = 0;
int last_target_port = 0;

int target_port = 15779;
int proxy_port = 31411;
char proxy_ip[64] = "127.0.0.1";

// --- Logging ---
void LogToFile(const char* format, ...) {
    char dllPath[MAX_PATH];
    GetModuleFileNameA(g_hModule, dllPath, MAX_PATH);
    char* lastSlash = strrchr(dllPath, '\\');
    if (lastSlash) *(lastSlash + 1) = '\0';
    strcat_s(dllPath, MAX_PATH, "redirector_log.txt");

    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsprintf_s(buffer, format, args);
    va_end(args);

    time_t now = time(0);
    struct tm ltm;
    localtime_s(&ltm, &now);
    char timeStr[32];
    strftime(timeStr, sizeof(timeStr), "[%H:%M:%S]", &ltm);

    FILE* f;
    if (fopen_s(&f, dllPath, "a") == 0) {
        fprintf(f, "%s %s\n", timeStr, buffer);
        fclose(f);
    }
    OutputDebugStringA(buffer);
}

void LoadConfig() {
    char configPath[MAX_PATH];
    GetModuleFileNameA(g_hModule, configPath, MAX_PATH);
    char* lastSlash = strrchr(configPath, '\\');
    if (lastSlash) *(lastSlash + 1) = '\0';
    strcat_s(configPath, MAX_PATH, "proxy_config.ini");

    target_port = GetPrivateProfileIntA("Proxy", "TargetPort", 15779, configPath);
    proxy_port = GetPrivateProfileIntA("Proxy", "ProxyPort", 31411, configPath);
    GetPrivateProfileStringA("Proxy", "ProxyIP", "127.0.0.1", proxy_ip, sizeof(proxy_ip), configPath);
    
    LogToFile("Config Loaded: TargetPort=%d, ProxyPort=%d, ProxyIP=%s", target_port, proxy_port, proxy_ip);
}

// --- Hooking Engine ---
void PlaceHook(void* addr, void* detour, BYTE* orig) {
    if (!addr) return;
    DWORD old;
    VirtualProtect(addr, 5, PAGE_EXECUTE_READWRITE, &old);
    memcpy(orig, addr, 5);
    DWORD rel = (DWORD)detour - (DWORD)addr - 5;
    *(BYTE*)addr = 0xE9;
    *(DWORD*)((BYTE*)addr + 1) = rel;
    VirtualProtect(addr, 5, old, &old);
}

void RemoveHook(void* addr, BYTE* orig) {
    if (!addr) return;
    DWORD old;
    VirtualProtect(addr, 5, PAGE_EXECUTE_READWRITE, &old);
    memcpy(addr, orig, 5);
    VirtualProtect(addr, 5, old, &old);
}

// --- Detours ---

int WSAAPI hooked_connect(SOCKET s, const struct sockaddr* name, int namelen) {
    if (name->sa_family == AF_INET) {
        struct sockaddr_in* addr = (struct sockaddr_in*)name;
        int port = ntohs(addr->sin_port);
        char ip[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &(addr->sin_addr), ip, INET_ADDRSTRLEN);

        if (port == target_port || (addr->sin_addr.S_un.S_addr != inet_addr("127.0.0.1") && port > 1000)) {
            LogToFile("connect() Hijack: %s:%d -> %s:%d", ip, port, proxy_ip, proxy_port);
            last_target_ip = addr->sin_addr.S_un.S_addr;
            last_target_port = port;
            addr->sin_port = htons(proxy_port);
            inet_pton(AF_INET, proxy_ip, &(addr->sin_addr));
        } else {
            LogToFile("connect() Pass: %s:%d", ip, port);
        }
    }

    RemoveHook(addr_connect, orig_connect);
    int res = connect(s, name, namelen);
    if (res == SOCKET_ERROR) {
        int err = WSAGetLastError();
        if (err != WSAEWOULDBLOCK) LogToFile("connect() Error: %d", err);
    }
    PlaceHook(addr_connect, (void*)hooked_connect, orig_connect);
    return res;
}

int WSAAPI hooked_WSAConnect(SOCKET s, const struct sockaddr* name, int namelen, LPWSABUF lpCallerData, LPWSABUF lpCalleeData, LPQOS lpSQOS, LPQOS lpGQOS) {
    if (name->sa_family == AF_INET) {
        struct sockaddr_in* addr = (struct sockaddr_in*)name;
        int port = ntohs(addr->sin_port);
        char ip[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &(addr->sin_addr), ip, INET_ADDRSTRLEN);

        if (port == target_port || (addr->sin_addr.S_un.S_addr != inet_addr("127.0.0.1") && port > 1000)) {
            LogToFile("WSAConnect() Hijack: %s:%d -> %s:%d", ip, port, proxy_ip, proxy_port);
            last_target_ip = addr->sin_addr.S_un.S_addr;
            last_target_port = port;
            addr->sin_port = htons(proxy_port);
            inet_pton(AF_INET, proxy_ip, &(addr->sin_addr));
        } else {
            LogToFile("WSAConnect() Pass: %s:%d", ip, port);
        }
    }

    RemoveHook(addr_WSAConnect, orig_WSAConnect);
    int res = WSAConnect(s, name, namelen, lpCallerData, lpCalleeData, lpSQOS, lpGQOS);
    if (res == SOCKET_ERROR) {
        int err = WSAGetLastError();
        if (err != WSAEWOULDBLOCK) LogToFile("WSAConnect() Error: %d", err);
    }
    PlaceHook(addr_WSAConnect, (void*)hooked_WSAConnect, orig_WSAConnect);
    return res;
}

// --- Entry Point ---

void InstallHooks() {
    HMODULE hWs2 = GetModuleHandleA("ws2_32.dll");
    if (!hWs2) hWs2 = LoadLibraryA("ws2_32.dll");
    if (!hWs2) return;

    addr_connect = GetProcAddress(hWs2, "connect");
    addr_WSAConnect = GetProcAddress(hWs2, "WSAConnect");

    PlaceHook(addr_connect, (void*)hooked_connect, orig_connect);
    PlaceHook(addr_WSAConnect, (void*)hooked_WSAConnect, orig_WSAConnect);
    
    LogToFile("Hooks installed (Manual JMP): connect, WSAConnect");
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    if (ul_reason_for_call == DLL_PROCESS_ATTACH) {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        
        char logPath[MAX_PATH];
        GetModuleFileNameA(hModule, logPath, MAX_PATH);
        char* lastSlash = strrchr(logPath, '\\');
        if (lastSlash) *(lastSlash + 1) = '\0';
        strcat_s(logPath, MAX_PATH, "redirector_log.txt");
        DeleteFileA(logPath);

        LogToFile("Redirector DLL (v2.3-Manual) Attached. PID: %d", GetCurrentProcessId());
        
        LoadConfig();
        InstallHooks();
    }
    return TRUE;
}
