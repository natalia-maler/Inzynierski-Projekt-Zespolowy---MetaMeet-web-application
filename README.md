# Inzynierski-Projekt-Zespolowy
## MetaMeet - Inteligentny system do umawiania spotkań pomiędzy firmami

[Prezentacja działania aplikacji](https://www.canva.com/design/DAG_64Qe9Mk/QBbDB13HXbI9NO1mwoTtqg/watch?utm_content=DAG_64Qe9Mk&utm_campaign=designshare&utm_medium=link2&utm_source=uniquelinks&utlId=he6c51cbb67)

MetaMeet to webowa aplikacja użytkowa wspomagająca organizację spotkań online w środowisku Microsoft 365.
Projekt został zrealizowany zespołowo w ramach pracy grupowej.

Celem systemu jest uproszczenie planowania spotkań zespołowych poprzez integrację z usługami Microsoft 365, w szczególności:
- Microsoft Graph
- Microsoft Outlook Calendar
- Microsoft Teams
- Azure Active Directory

Aplikacja umożliwia szybkie sprawdzanie dostępności współpracowników, wybór wspólnego terminu oraz automatyczne tworzenie spotkań Microsoft Teams wraz z wysłaniem zaproszeń do uczestników. System eliminuje konieczność ręcznego przeglądania kalendarzy i znacząco skraca czas potrzebny na koordynację spotkań.

## Główne funkcjonalności
**Uwierzytelnianie użytkownika**
- Logowanie przy użyciu konta Microsoft 365.
- Integracja z Microsoft Entra ID (Azure AD).
- Autoryzacja oparta o OAuth 2.0 / OpenID Connect.
- Rejestracja aplikacji w portalu Azure oraz konfiguracja uprawnień do Microsoft Graph.

**Zarządzanie profilem użytkownika**
- Wyświetlanie podstawowych danych (imię, nazwisko, e-mail, stanowisko).
- Możliwość zmiany nazwy wyświetlanej.
- Podgląd statusu licencji Microsoft 365.

**Zarządzanie zespołem**
- Pobieranie listy użytkowników organizacji z Microsoft Graph.
- Wybór uczestników spotkania z poziomu aplikacji.

**Planowanie spotkań**
- Wybór uczestników.
- Określenie daty, godziny rozpoczęcia i czasu trwania.
- Automatyczne sprawdzanie dostępności kalendarzy wszystkich uczestników.
- Generowanie sugerowanych wolnych terminów.

**Tworzenie spotkań Microsoft Teams**
- Automatyczne tworzenie spotkania Teams przez Microsoft Graph.
- Wysłanie zaproszeń do uczestników.
- Zapis wydarzenia w kalendarzu Outlook.

**Panel ustawień**
- Podgląd statusu licencji Microsoft 365.
- Szybki dostęp do ustawień profilu.

**Tworzenie nowego użytkownika**
- Możliwość utworzenia nowego konta użytkownika z poziomu aplikacji (z wykorzystaniem Microsoft Graph i odpowiednich uprawnień aplikacyjnych).

## Mój zakres odpowiedzialności w projekcie MetaMeet

1. **Konfiguracja środowiska Microsoft 365 i Entra ID**
   - Utworzenie konta administratora Microsoft 365 z aktywną płatną subskrypcją w celu umożliwienia korzystania z zaawansowanych funkcjonalności Microsoft Graph API.
   - Rejestracja aplikacji w Azure Active Directory.
   - Nadzór nad formalnymi aspektami integracji, w tym: nadawanie odpowiednich uprawnień aplikacyjnych i delegowanych, konfiguracja zakresów (scopes), obsługa zgód administracyjnych,

2. **Integracja z Microsoft Graph API**
   - Implementacja mechanizmu komunikacji aplikacji z Microsoft Graph API.
   - Odpowiedzialność za poprawne pozyskiwanie tokenów dostępu OAuth 2.0.

3. **Obsługa logowania i autoryzacji użytkowników**
   - Implementacja logowania użytkowników przy użyciu kont Microsoft 365.

4. **Logika analizy dostępności kalendarzy**
   - Analiza dostępności kalendarzy użytkowników.
   - Porównywanie terminów pomiędzy członkami zespołu.
   - Wybór użytkowników do spotkań.

5. **Automatyczne przypisywanie licencji Microsoft 365**
   - Implementacja logiki automatycznego przypisywania licencji podczas tworzenia nowego użytkownika.

6. **Implementacja zakładki "Moje spotkania"**
   - Zaprojektowanie i implementacja zakładki umożliwiającej zarządzanie spotkaniami (anulowanie, odrzucanie, dołączanie, przywracanie).
   - Integracja widoku z logiką Microsoft Graph API oraz synchronizacja z kalendarzem Outlook.
