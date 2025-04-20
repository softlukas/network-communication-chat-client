# IPK project 2

## Obsah

* Využitie umelej inteligencie
* Implementácia
* Zdrojové súbory
* Testovanie
* Ilustračná fukcionalita
* Zdroje

## Implementácia

### Zdrojové súbory

#### ArgsParser

#### Program

[source: 1] Je štartovacím bodom celej aplikácie, je zodpovedný za spracovanie vstupných argumentov a vytvorenie objektu TcpClient alebo UdpClient.

#### Message

[source: 2] Toto je hlavná rodičovská trieda, z ktorej dedia všetky typy správ. [source: 3] Obsahuje abstraktný typ `MessageType`, ktorý si každá odvodená trieda nastaví podľa typu správy, ktorý reprezentuje.

[source: 4] Ďalej táto trieda obsahuje predpisy dvoch metód `GetBytesInTcpGrammar` a `GetBytesForUdpPacket`, ktoré sú zodpovedné za navrátenie správneho obsahu pre poslanie, ktorý obsahuje atribúty definované na danej správe, ako je napr. [source: 5] `MessageContent` či `Displayname`. Tieto metódy si každá trieda voliteľne implementuje podľa potreby, kedže nie všetky typy správ sa používajú v oboch protokoloch.

[source: 6] Ďalej táto základná trieda obsahuje dva typy konštruktorov, s a bez použitia `messageId`. Každý sa používa v inom protokole.

[source: 7] Na koniec trieda message obsahuje ešte validačné metódy, ktoré kontrolujú užívateľský vstup.

[source: 8] **Odvodené triedy od triedy Message:**

* AuthMessage
* ByeMessage
* ErrMessage
* JoinMessage
* MsgMessage
* PingMessage
* ConfrimMessage
* ReplyAuthMessage (používa sa aj pri odpovedi na správu Join)

#### ChatClient

Táto trieda zastupuje vlastnosti ktoré sú rovnaké pre oba protokoly, ako sú port, cieľový server, aktuálny stav podľa FSM v ktorom sa klient aktuálne nachádza. [source: 9] V tomto súbore je definovaný zoznam všetkých možných stavov klienta podľa FSM.

#### TcpChatClient

[source: 10] Metóda `Start` je vstupným bodom, nachádza sa v nej logika FSM, a podľa užívateľského vstupu vytvorenie príslušných správ v pomocných funkciách.

[source: 11] Na začiatku sa zavolá metóda `ConnectAsync` a spustí sa príjimacia slučka.

[source: 12] Metóda `ProcessMessageAsync` potom odošle príslušnú správu volaním funkcie `SendPalyoadAsync` ktorá ako parameter bere už byty danej správy.

[source: 13] Prichádzajúce správy spracúva funkcia `ReceiveLoopAsync`, ktorá predá prichádzajúcu správu parseru. [source: 14] Prijatá správa je potom vypísaná pomocou `ToString()` metódy volanej na vytvorenom objekte danej správy.

#### UdpChatClient

Má podobnú logiku ako TCP.

[source: 15] Miesto metódy `ConnectAsync` je tu metóda `InitializeSocket`.

Slovník `_pendingConfirmationMessages` kde kľúč je `MessageId` drží informácie o správach aktuálne čakajúcich na potvrdenie. [source: 16] Ako value obsahuje objek `SentMessageInfo`, kde je uložená správa ktorá bola poslaná pod týmto ID, a umožňuje tak jednoduché znovupreposlanie.

[source: 17] `HashSet _pendingConfirmationMessages` zase uchováva informácie o správach, ktoré už boli spracované, pre vyhnutie sa duplicitnému spracovaniu rovnakej správy.

[source: 18] Prijímacia slučka `ReceiveLoopAsync` posiela CONFIRM ešte pred spracovaním správy parserom, zároveň kontroluje či daná správa už nebola spracovaná.

#### SentMessageInfo

// todo

#### TcpMessageParser / UdpMessageParser

Statická trieda,

## Testovanie

[source: 19] Počas vývoja som projekt testoval hlavne pomocou testovacích python serverov, ktoré som použil so súhlasom autora. [source: 20] [odkaz na zdroj]

[source: 21] V konečnej fáze som použil testy vytvorené inými študentami, ako finálna kontrola mojej implementácie. Problém, ktorý sa vyskytol v tejto časti je spomenutý v súbore CHANGELOG. [Odkaz na zdroj]

![Alt text](img/student_tests.png "Voliteľný titulok")





## Ilustračná fukcionalita

## Zdroje
