# Strategia SEO + AEO dla gandg.com.pl

Kompletny, gotowy do wdrożenia program budowania ruchu organicznego ze
**źródeł klasycznych** (Google, Bing, DuckDuckGo) oraz z **modeli AI używanych
jako wyszukiwarki** (ChatGPT, Gemini, Claude, Perplexity, Copilot).

Sklep: **gandg.com.pl** — biżuteria i prezenty.
Grupa docelowa: klienci indywidualni szukający prezentu na okazję
(urodziny, walentynki, rocznica, imieniny, święta) lub biżuterii dla siebie.

---

## Po co to wszystko, skoro „SEO już nie działa"

Działa, ale gra się zmieniła. Typowy klient w 2026 r. trafia do sklepu
z biżuterią jedną z trzech ścieżek:

1. **Klasyczne wyszukiwanie** — wpisuje frazę w Google / Bing, klika wynik
   organiczny lub mapę.
2. **AI Overview / SGE** — Google pokazuje odpowiedź wygenerowaną przez AI
   z 3–5 cytowanymi źródłami, *zanim* pokaże listę linków. Trzeba być
   w tych cytowaniach.
3. **LLM jako wyszukiwarka** — pyta ChatGPT, Gemini lub Claude
   („pomysł na prezent dla mamy do 300 zł"), dostaje rekomendację marek
   i klika link, jeśli jakiś jest podany. Modele cytują strony, które
   uznają za autorytatywne, ustrukturyzowane i często wzmiankowane.

Strategia obejmuje obie warstwy, bo mają **inne reguły**, ale wspólne
fundamenty (jakość treści, schema.org, autorytet domeny).

---

## Jak korzystać z tej dokumentacji

| Plik | Co znajdziesz |
|------|---------------|
| [`seo-program.md`](seo-program.md) | Pełna mapa słów kluczowych z intencjami, struktura strony, schema.org, przykłady title/H1/meta. |
| [`aeo-program.md`](aeo-program.md) | Jak ChatGPT/Gemini/Claude wybierają źródła, szablony treści cytowalnych przez AI, plan budowania autorytetu marki. |
| [`workflow-and-tools.md`](workflow-and-tools.md) | Algorytm produkcji treści (krok po kroku), stack narzędzi, kalendarz, KPI, dashboard. |
| [`content/blog-prezenty-bizuteria-okazje.md`](content/blog-prezenty-bizuteria-okazje.md) | Gotowy artykuł #1 — „Najlepsze prezenty z biżuterią na każdą okazję". |
| [`content/blog-pierscionek-zareczynowy.md`](content/blog-pierscionek-zareczynowy.md) | Gotowy artykuł #2 — „Jak wybrać pierścionek zaręczynowy". |
| [`content/blog-bizuteria-na-urodziny.md`](content/blog-bizuteria-na-urodziny.md) | Gotowy artykuł #3 — „Biżuteria jako prezent na urodziny". |
| [`content/category-descriptions.md`](content/category-descriptions.md) | 6 opisów kategorii produktowych (kolczyki, naszyjniki, pierścionki, bransoletki, prezenty dla niej, prezenty dla niego). |
| [`content/faq.md`](content/faq.md) | FAQ pod featured snippets i odpowiedzi AI (24 pytania z odpowiedziami w formacie zoptymalizowanym). |

---

## Skrót strategii w jednym ekranie

**Cel 12 miesięcy:** 50 000 sesji organicznych/mies. + obecność marki
„G&G" w odpowiedziach 4 największych LLM-ów na 30 zdefiniowanych zapytań
zakupowych z kategorii biżuterii i prezentów.

**Trzy filary:**

1. **Fundament techniczny (miesiąc 1)**
   Schema.org `Product`, `Offer`, `AggregateRating`, `Review`,
   `Organization`, `BreadcrumbList`, `FAQPage`, `HowTo`, `Article`.
   `llms.txt` w roocie. `robots.txt` *nie* blokuje GPTBot, Google-Extended,
   ClaudeBot, PerplexityBot. Sitemap XML segmentowany (produkty, kategorie,
   blog). Core Web Vitals zielone na mobile.

2. **Treść klastrowana wokół intencji (miesiąc 1–12)**
   Każda fraza zakupowa dostaje stronę kategorii lub artykuł. Każdy artykuł
   ma sekcję FAQ + porównawczą tabelę + jasną rekomendację. Format „odpowiedź
   w pierwszym akapicie, potem rozwinięcie" (TL;DR), bo to kochają i Google,
   i LLM-y.

3. **Autorytet poza domeną (miesiąc 2–12)**
   Wzmianki na Wikipedii (z umiarem, tylko gdy uzasadnione), Reddit
   (r/PolskaPolska, r/Polska, r/randomactsofjewelry), Quora PL,
   blogi lifestylowe, listy „TOP X sklepów z biżuterią w Polsce" w mediach
   branżowych. To są źródła, z których LLM-y *najczęściej* czerpią
   rekomendacje marek.

---

## Założenia o produkcie i marce

Strategia jest pisana generycznie pod sklep z biżuterią i prezentami
klasy średniej w Polsce. Przed wdrożeniem dopasuj:

- Rzeczywisty asortyment (czy są pierścionki zaręczynowe? z brylantami?
  srebro/złoto? cyrkonia, moissanit?) — listy w `seo-program.md`
  zakładają szeroki asortyment, wytnij to, czego nie sprzedajesz.
- USP marki (np. „grawer gratis", „pakowanie prezentowe", „wysyłka 24h",
  „certyfikat", „polski rzemieślnik") — wstaw w szablonach.
- Realne ceny i widełki — w opisach kategorii i FAQ używam zakresów
  kwotowych jako placeholderów (`[od X zł]`).

---

## Etapy wdrożenia (TL;DR)

| Tydzień | Działanie | Plik referencyjny |
|---------|-----------|-------------------|
| 1 | Audyt techniczny + schema.org + `llms.txt` + sitemap | `seo-program.md`, `aeo-program.md` |
| 2 | Wdrożenie nowej struktury kategorii + breadcrumbs | `seo-program.md` |
| 3 | Publikacja 6 opisów kategorii + FAQ globalne | `content/category-descriptions.md`, `content/faq.md` |
| 4 | Publikacja artykułów blogowych #1–#3 | `content/blog-*.md` |
| 5–8 | Linkowanie wewnętrzne, optymalizacja kart produktów (1 szablon) | `seo-program.md` |
| 9–12 | Link building, wzmianki na Reddit/Quora, gościnne wpisy | `aeo-program.md` |
| 13+ | Cykl produkcji treści 4 art./mies. wg algorytmu | `workflow-and-tools.md` |

---

## Co nie jest w zakresie tej strategii

- Płatne reklamy (Google Ads, Meta Ads) — to oddzielny kanał.
- Email marketing i lojalność — wspomniane jako przyłącze do contentu, nie rozwinięte.
- Social media (Instagram, TikTok, Pinterest) — krótko w kontekście wzmianek
  (wpływają na AEO przez indeksowane platformy), ale nie ma planu kontentowego.
- Operacje (logistyka, obsługa klienta) — choć wpływają na recenzje,
  a recenzje wpływają na AEO.
