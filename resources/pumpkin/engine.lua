local DAMAGE = {
    { path = "/Game/COS_Entity.COS_Entity_C:DealDamages", health = "Health", entity = true },
    { path = "/Game/Animated/Bat.Bat_C:DealDamages", health = "heal" },
    { path = "/Game/Animated/Slime.Slime_C:DealDamages", health = "heal" },
    { path = "/Game/Animated/GhostKnight.GhostKnight_C:DealDamages", health = "heal" }
}
local COLLECTIBLES = {"pickableSkull_C", "BP_Gramophone_C"}
local JACK = "/Game/Animated/Scarecrow/Scarecrow.Scarecrow_C:"
local MOVEMENT = { JACK .. "F_AutoRun", JACK .. "F_SetSpeed" }
local M = {}

local function valid(obj)
    return obj and obj:IsValid()
end

local function playable(pawn)
    return valid(pawn) and pawn:GetClass():GetFName():ToString() == "Scarecrow_C"
        and not pawn.isMainMenu and pawn["isHeadAttached?"] and pawn.scarecrowHeal > 0
end

function M.targets(pawn, controller)
    if not playable(pawn) then return {} end
    local camera, world = controller.PlayerCameraManager, pawn:GetWorld()
    if not valid(camera) or not valid(world) then return {} end
    local origin, yaw = pawn:K2_GetActorLocation(), camera:GetCameraRotation().Yaw
    local address = world:GetAddress()
    local targets = {}
    for index, class in ipairs(COLLECTIBLES) do
        local nearest, angle
        for _, item in ipairs(FindAllOf(class) or {}) do
            if valid(item) and not item.bHidden and item.bActorEnableCollision then
                local itemWorld = item:GetWorld()
                if valid(itemWorld) and itemWorld:GetAddress() == address then
                    local location = item:K2_GetActorLocation()
                    local x, y, z = location.X - origin.X, location.Y - origin.Y, location.Z - origin.Z
                    local distance = x*x + y*y + z*z
                    if not nearest or distance < nearest then
                        nearest = distance
                        angle = (math.deg(math.atan(y, x)) - yaw + 180) % 360 - 180
                    end
                end
            end
        end
        if nearest then targets[index] = { math.floor(math.sqrt(nearest)/100 + 0.5), math.floor(angle + 0.5) } end
    end
    return targets
end

function M.new()
    local state = { pawn = nil, saved = {}, enabled = {}, damageHook = false, hooks = {}, finishing = false }

    function state:restore(key)
        local entry = self.saved[key]
        self.saved[key] = nil
        if entry and valid(entry.object) and entry.object[key] == entry.written then
            entry.object[key] = entry.original
        end
    end

    function state:override(object, key, value)
        local current = object[key]
        local entry = self.saved[key]
        if not entry or current ~= entry.written then
            entry = { object = object, original = current }
            self.saved[key] = entry
        end
        if current ~= value then object[key] = value end
        entry.written = object[key]
    end

    function state:clear()
        for _, key in ipairs({"isGodMode", "MaxWalkSpeed"}) do
            self:restore(key)
        end
        self.enabled = {}
    end

    function state:applySpeed(pawn)
        local movement = pawn.CharacterMovement
        if not self.enabled.speed or not movement:IsMovingOnGround()
            or pawn["isRolling?"] or pawn.movementBlocked then
            self:restore("MaxWalkSpeed")
            return
        end
        local original = movement.MaxWalkSpeed
        local saved = self.saved.MaxWalkSpeed
        if saved and original == saved.written then original = saved.original end
        self:override(movement, "MaxWalkSpeed", original * self.enabled.multiplier)
    end

    function state:update(pawn, options)
        local address = valid(pawn) and pawn:GetAddress() or nil
        if self.address ~= address then
            self:clear()
            self.address = address
        end
        self.pawn = pawn
        self.enabled = options
        if not playable(pawn) then
            self:clear()
            return false
        end
        if options.health then
            self:override(pawn, "isGodMode", true)
        else
            self:restore("isGodMode")
        end
        if options.jump and pawn.jumpCount > 1 and pawn.CharacterMovement:IsFalling() then
            pawn.jumpCount = 1
        end
        if options.crow and pawn.crowReload < 1 then pawn.crowReload = 1 end
        self:applySpeed(pawn)
        return true
    end

    function state:hookDamage(definition)
        RegisterHook(definition.path, function(context, amount, protection, dealer, important, ignoreRoll, special)
            if self.finishing or not self.enabled.oneHit or not playable(self.pawn) then return end
            local enemy, source = context:get(), dealer:get()
            if not valid(enemy) or not valid(source) or amount:get() <= 0
                or source:GetAddress() ~= self.pawn:GetAddress()
                or (definition.entity and (not enemy.isInit or enemy.isCustomHit))
                or enemy[definition.health] <= 0 then return end
            local protect, critical, ignore, playSound = protection:get(), important:get(), ignoreRoll:get(), special:get()
            ExecuteInGameThread(function()
                if not self.enabled.oneHit or not valid(source) or not valid(enemy)
                    or source:GetAddress() ~= self.address or enemy[definition.health] <= 0 then return end
                self.finishing = true
                local ok, failure = pcall(function()
                    enemy:DealDamages(enemy[definition.health], protect, source, critical, ignore, playSound)
                end)
                self.finishing = false
                if not ok then print("[TrainerHub] Dano: " .. tostring(failure) .. "\n") end
            end)
        end)
        self.hooks[definition.path] = true
        self.damageHook = true
    end

    function state:installHooks()
        for _, functionPath in ipairs(MOVEMENT) do
            if not self.hooks[functionPath] and valid(StaticFindObject(functionPath)) then
                local ok = pcall(RegisterHook, functionPath, function(context)
                    if not self.enabled.speed then return end
                    local pawn = context:get()
                    if valid(pawn) and pawn:GetAddress() == self.address and playable(pawn) then
                        self:applySpeed(pawn)
                    end
                end)
                self.hooks[functionPath] = ok
            end
        end
        for _, definition in ipairs(DAMAGE) do
            if not self.hooks[definition.path] and valid(StaticFindObject(definition.path)) then
                pcall(function() self:hookDamage(definition) end)
            end
        end
    end

    return state
end

return M
