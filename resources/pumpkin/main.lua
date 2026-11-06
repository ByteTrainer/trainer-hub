local source = debug.getinfo(1, "S").source:sub(2):gsub("\\", "/")
local module = dofile(source:match("^(.*)/") .. "/engine.lua")
local engine = module.new()
local root = assert(os.getenv("TEMP")) .. "/trainer-hub-pumpkin-"
local pending, ticks = false, 0

local function settings()
    local file = io.open(root .. "settings.txt", "r")
    if not file then return {} end
    local raw = file:read(300)
    file:close()
    local expiry, token, health, jump, speed, hit, crow, tracker, multiplier = (raw or ""):match(
        "^(%d+) ([a-f0-9]+) ([01]) ([01]) ([01]) ([01]) ([01]) ([01]) (%d+)\n?$")
    local now = os.time()
    expiry, multiplier = tonumber(expiry), tonumber(multiplier)
    if not expiry or expiry <= now or expiry > now + 5 or #token ~= 32
        or multiplier < 50 or multiplier > 250 then return {} end
    return { token = token, health = health == "1", jump = jump == "1",
        speed = speed == "1", oneHit = hit == "1", crow = crow == "1",
        tracker = tracker == "1", multiplier = multiplier / 100 }
end

local function status(ready, token, failed, targets)
    local file = io.open(root .. "status.tmp", "w")
    if not file then return end
    file:write("2 ", tostring(os.time()), " ", ready and "1" or "0", " ",
        engine.damageHook and "1" or "0", " ", token or "-", " ", failed and "1" or "0")
    for index = 1, 2 do
        local target = targets[index]
        file:write(" ", target and tostring(target[1]) or "-1", " ", target and tostring(target[2]) or "0")
    end
    file:close()
    os.remove(root .. "status.txt")
    os.rename(root .. "status.tmp", root .. "status.txt")
end

LoopAsync(50, function()
    if pending then return false end
    pending = true
    ticks = ticks + 1
    if ticks % 20 == 1 then
        pcall(function() engine:installHooks() end)
    end
    local options = settings()
    ExecuteInGameThread(function()
        local controller, pawn
        local ok, ready = pcall(function()
            controller = FindFirstOf("PlayerController")
            pawn = controller and controller:IsValid() and controller.Pawn or nil
            return engine:update(pawn, options)
        end)
        if not ok then
            pcall(function() engine:clear() end)
            print("[TrainerHub] " .. tostring(ready) .. "\n")
        end
        if ticks % (options.tracker and 4 or 16) == 1 then
            local targets = {}
            if ok and ready and options.tracker then
                local found, result = pcall(module.targets, pawn, controller)
                if found then targets = result end
            end
            status(ok and ready, options.token, not ok, targets)
        end
        pending = false
    end)
    return false
end)
