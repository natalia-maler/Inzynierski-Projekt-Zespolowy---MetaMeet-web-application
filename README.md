# Inzynierski-Projekt-Zespolowy
## MetaMeet

MetaMeet to webowa aplikacja użytkowa wspomagająca organizację spotkań online w środowisku Microsoft 365.
Projekt został zrealizowany zespołowo w ramach pracy grupowej.

Celem systemu jest uproszczenie planowania spotkań zespołowych poprzez integrację z usługami Microsoft 365, w szczególności:
- Microsoft Graph
- Microsoft Outlook Calendar
- Microsoft Teams
- Azure Active Directory

Aplikacja umożliwia szybkie sprawdzanie dostępności współpracowników, wybór wspólnego terminu oraz automatyczne tworzenie spotkań Microsoft Teams wraz z wysłaniem zaproszeń do uczestników. System eliminuje konieczność ręcznego przeglądania kalendarzy i znacząco skraca czas potrzebny na koordynację spotkań.

# Główne funkcjonalności
Uwierzytelnianie użytkownika
- Logowanie przy użyciu konta Microsoft 365.
- Integracja z Microsoft Entra ID (Azure AD).
- Autoryzacja oparta o OAuth 2.0 / OpenID Connect.
- Rejestracja aplikacji w portalu Azure oraz konfiguracja uprawnień do Microsoft Graph.

Zarządzanie profilem użytkownika
- Wyświetlanie podstawowych danych (imię, nazwisko, e-mail, stanowisko).
- Możliwość zmiany nazwy wyświetlanej.
- Podgląd statusu licencji Microsoft 365.

Zarządzanie zespołem
- Pobieranie listy użytkowników organizacji z Microsoft Graph.
- Wybór uczestników spotkania z poziomu aplikacji.

Planowanie spotkań
- Wybór uczestników.
- Określenie daty, godziny rozpoczęcia i czasu trwania.
- Automatyczne sprawdzanie dostępności kalendarzy wszystkich uczestników.
- Generowanie sugerowanych wolnych terminów.

Tworzenie spotkań Microsoft Teams
- Automatyczne tworzenie spotkania Teams przez Microsoft Graph.
- Wysłanie zaproszeń do uczestników.
- Zapis wydarzenia w kalendarzu Outlook.

Panel ustawień
- Podgląd statusu licencji Microsoft 365.
- Szybki dostęp do ustawień profilu.

Tworzenie nowego użytkownika
- Możliwość utworzenia nowego konta użytkownika z poziomu aplikacji (z wykorzystaniem Microsoft Graph i odpowiednich uprawnień aplikacyjnych).
