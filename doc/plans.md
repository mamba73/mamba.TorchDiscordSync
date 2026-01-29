# 🔫 DEATH DETECTION IMPLEMENTATION PLAN
## 1. Proširiti PlayerTrackingService sa death event subscription
Treba dodati:

Event subscription za character death eventove
Metodu za obradu death informacija
Integraciju sa DeathLogService

## 2. Ažurirati PlayerTrackingService konstruktor
public PlayerTrackingService(EventLoggingService eventLog, ITorchBase torch, DeathLogService deathLog)
{
    _eventLog = eventLog;
    _torch = torch;
    _deathLog = deathLog; // NOVI PARAMETAR
}

## 3. Dodati death event handler metodu
Metoda koja će hvatati MyCharacter.Kill() eventove i pozivati _deathLog.LogPlayerDeathAsync()

## 4. Ažurirati Plugin main class
Ažurirati instanciranje PlayerTrackingService da uključi DeathLogService.