<script lang="ts">
	import { page } from '$app/stores';
	import { FORLOB_PLACEHOLDER } from '$lib/data/forlob';

	const slug = $derived($page.params.slug);

	const forlob = $derived(FORLOB_PLACEHOLDER.find((f) => f.slug === slug));

	const prevForlob = $derived(
		forlob ? FORLOB_PLACEHOLDER.find((f) => f.rækkefølge === forlob.rækkefølge - 1) : null
	);
	const nextForlob = $derived(
		forlob ? FORLOB_PLACEHOLDER.find((f) => f.rækkefølge === forlob.rækkefølge + 1) : null
	);
</script>

{#if !forlob}
	<div class="container">
		<h1>Forløb ikke fundet</h1>
		<p>Der findes intet forløb med slug "{slug}".</p>
		<a href="/" class="btn">Tilbage til roadmap</a>
	</div>
{:else}
	{@const indhold = forlob.indhold}
	<div class="container forlob-page">
		<nav class="breadcrumb">
			<a href="/">Roadmap</a>
			<span>/</span>
			<span>{forlob.titel}</span>
		</nav>

		<header class="forlob-header">
			<span class="forlob-label">{forlob.slug.toUpperCase()}</span>
			<h1>{forlob.titel}</h1>
			{#if forlob.beskrivelse}
				<p class="forlob-beskrivelse">{forlob.beskrivelse}</p>
			{/if}
		</header>

		{#if indhold.svendeprøve}
			<section class="card">
				<h2>Svendeprøveforløb</h2>
				{#if indhold.svendeprøve.ugeoverskrift}
					<p class="muted">{indhold.svendeprøve.ugeoverskrift}</p>
				{/if}

				<div class="week-grid" role="list">
					{#each indhold.svendeprøve.uger as w}
						<div class="week" role="listitem">
							<div class="week-top">
								<span class="week-number">Uge {w.uge}</span>
								<span class="week-label">{w.label}</span>
							</div>
						</div>
					{/each}
				</div>
			</section>

			<section class="card">
				<h3>Delprøver</h3>
				<ul class="bullets">
					{#each indhold.svendeprøve.delprøver as d}
						<li>{d}</li>
					{/each}
				</ul>
			</section>

			<section class="card">
				<h3>Dokumentation</h3>
				<div class="doc-grid">
					{#each indhold.svendeprøve.dokumentation as doc}
						<div class="doc">
							<h4>{doc.titel}</h4>
							<p class="muted">{doc.beskrivelse}</p>
						</div>
					{/each}
				</div>
			</section>

			{#if indhold.svendeprøve.vejledning}
				<section class="card">
					<h3>Vejledning og facilitering</h3>
					<p>{indhold.svendeprøve.vejledning}</p>
				</section>
			{/if}

			{#if indhold.svendeprøve.lovgivning}
				<section class="card">
					<h3>Lovgrundlag</h3>
					<p class="muted">{indhold.svendeprøve.lovgivning}</p>
				</section>
			{/if}
		{:else}
			{#if indhold.mål}
				<section class="card">
					<h2>Mål for undervisningen</h2>
					<p>{indhold.mål.formål}</p>
					<h3>Efter forløbet kan eleven:</h3>
					<ul class="bullets">
						{#each indhold.mål.efter_forløbet as item}
							<li>{item}</li>
						{/each}
					</ul>
				</section>
			{/if}

			{#if indhold.indhold_undervisning}
				<section class="card">
					<h2>Indhold i undervisningen</h2>
					{#if indhold.indhold_undervisning.tekst}
						<p>{indhold.indhold_undervisning.tekst}</p>
					{/if}

					{#if indhold.indhold_undervisning.temaer?.length}
						<h3>Hovedtemaer</h3>
						<ul class="bullets">
							{#each indhold.indhold_undervisning.temaer as tema}
								<li>{tema}</li>
							{/each}
						</ul>
					{/if}

					{#if indhold.indhold_undervisning.projektnavn}
						<div class="callout">
							<span class="callout-label">Projekt</span>
							<strong>{indhold.indhold_undervisning.projektnavn}</strong>
						</div>
					{/if}
				</section>
			{/if}

			{#if indhold.helhedsorientering}
				<section class="card">
					<h2>Helhedsorientering</h2>
					<p>{indhold.helhedsorientering}</p>
				</section>
			{/if}

			{#if indhold.praksisrelation}
				<section class="card">
					<h2>Praksisrelation</h2>
					<p>{indhold.praksisrelation}</p>
				</section>
			{/if}

			{#if indhold.tværfaglighed}
				<section class="card">
					<h2>Tværfaglighed</h2>
					<p>{indhold.tværfaglighed}</p>
				</section>
			{/if}

			{#if indhold.differentiering}
				<section class="card">
					<h2>Differentiering</h2>
					<p>{indhold.differentiering}</p>
				</section>
			{/if}

			{#if indhold.evaluering?.læringsaktiviteter?.length}
				<section class="card">
					<h2>Evaluering og bedømmelse</h2>
					<p class="muted">
						Herunder ses læringsaktiviteterne i forløbet. Under hver aktivitet fremgår mål, indhold og hvad der
						typisk gives feedback på.
					</p>

					<div class="activity-list">
						{#each indhold.evaluering.læringsaktiviteter as akt}
							<details class="activity">
								<summary>
									<span class="activity-title">{akt.titel}</span>
									<span class="activity-meta">{akt.lektioner} lektioner</span>
								</summary>

								<div class="activity-body">
									<h4>Læringsmål</h4>
									<ul class="bullets">
										{#each akt.læringsmål as m}
											<li>{m}</li>
										{/each}
									</ul>

									<div class="two-col">
										<div>
											<h4>Teoretisk del</h4>
											<ul class="bullets">
												{#each akt.indholdselementer.teoretisk_del as t}
													<li>{t}</li>
												{/each}
											</ul>
										</div>
										<div>
											<h4>Praktisk del</h4>
											<ul class="bullets">
												{#each akt.indholdselementer.praktisk_del as p}
													<li>{p}</li>
												{/each}
											</ul>
										</div>
									</div>

									<h4>Feedback</h4>
									<p class="muted">{akt.feedback_bedommelse.undervisere_feedback}</p>

									<h4>Ved bedømmelsen lægges der vægt på</h4>
									<p class="muted">{akt.feedback_bedommelse.ved_bedommelsen}</p>
								</div>
							</details>
						{/each}
					</div>
				</section>
			{/if}

			{#if !indhold.mål && !indhold.indhold_undervisning && !indhold.evaluering}
				<section class="placeholder">
					<p>Indhold for dette forløb kommer snart.</p>
				</section>
			{/if}
		{/if}

		<nav class="forlob-nav">
			{#if prevForlob}
				<a href="/forlob/{prevForlob.slug}" class="btn btn-secondary">← {prevForlob.titel}</a>
			{/if}
			<a href="/" class="btn">Til roadmap</a>
			{#if nextForlob}
				<a href="/forlob/{nextForlob.slug}" class="btn btn-secondary">{nextForlob.titel} →</a>
			{/if}
		</nav>
	</div>
{/if}

<style>
	.breadcrumb {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		margin-bottom: 2rem;
		color: var(--grå-tekst);
		font-size: 0.9rem;
	}

	.breadcrumb a:hover {
		color: var(--hvid);
	}

	.forlob-header {
		margin-bottom: 2rem;
	}

	.forlob-label {
		display: inline-block;
		font-weight: 700;
		font-size: 0.85rem;
		color: var(--mercantec-blå-lys);
		margin-bottom: 0.5rem;
	}

	.forlob-header h1 {
		font-size: 2rem;
		margin: 0 0 0.5rem;
	}

	.forlob-beskrivelse {
		color: var(--grå-tekst);
		font-size: 1.1rem;
		margin: 0;
	}

	.placeholder {
		background: rgba(255, 255, 255, 0.05);
		border-radius: 0.5rem;
		padding: 2rem;
		margin-bottom: 2rem;
	}

	.placeholder p {
		margin: 0;
		color: var(--grå-tekst);
	}

	.card {
		background: rgba(255, 255, 255, 0.06);
		border: 1px solid rgba(255, 255, 255, 0.12);
		border-radius: 0.75rem;
		padding: 1.75rem;
		margin-bottom: 1.25rem;
		box-shadow: 0 10px 24px rgba(0, 0, 0, 0.12);
	}

	.card h2 {
		margin: 0 0 0.75rem;
		font-size: 1.4rem;
	}

	.card h3 {
		margin: 1.25rem 0 0.5rem;
		font-size: 1.1rem;
	}

	.card h4 {
		margin: 1rem 0 0.5rem;
		font-size: 1rem;
	}

	.muted {
		color: var(--grå-tekst);
		margin: 0.5rem 0 0;
	}

	.bullets {
		margin: 0.5rem 0 0;
		padding-left: 1.2rem;
	}

	.bullets li {
		margin: 0.25rem 0;
		color: var(--grå-lys);
	}

	.callout {
		margin-top: 1rem;
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		gap: 0.5rem;
		padding: 0.85rem 1rem;
		border-radius: 0.6rem;
		border: 1px solid rgba(59, 130, 246, 0.45);
		background: rgba(59, 130, 246, 0.12);
	}

	.callout-label {
		font-size: 0.75rem;
		letter-spacing: 0.08em;
		text-transform: uppercase;
		font-weight: 700;
		color: var(--mercantec-blå-lys);
	}

	.activity-list {
		margin-top: 1rem;
		display: grid;
		gap: 0.75rem;
	}

	details.activity {
		border-radius: 0.65rem;
		border: 1px solid rgba(255, 255, 255, 0.12);
		background: rgba(0, 0, 0, 0.12);
		overflow: hidden;
	}

	details.activity summary {
		cursor: pointer;
		list-style: none;
		display: flex;
		align-items: baseline;
		justify-content: space-between;
		gap: 1rem;
		padding: 0.9rem 1rem;
	}

	details.activity summary::-webkit-details-marker {
		display: none;
	}

	.activity-title {
		font-weight: 700;
	}

	.activity-meta {
		color: var(--grå-tekst);
		font-size: 0.9rem;
		white-space: nowrap;
	}

	.activity-body {
		padding: 0 1rem 1rem;
	}

	.two-col {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1rem;
		margin-top: 0.5rem;
	}

	.week-grid {
		margin-top: 1rem;
		display: grid;
		grid-template-columns: repeat(5, minmax(0, 1fr));
		gap: 0.75rem;
	}

	.week {
		border-radius: 0.65rem;
		border: 1px solid rgba(255, 255, 255, 0.12);
		background: rgba(0, 0, 0, 0.12);
		padding: 0.9rem 1rem;
	}

	.week-top {
		display: grid;
		gap: 0.35rem;
	}

	.week-number {
		font-weight: 700;
	}

	.week-label {
		color: var(--grå-tekst);
	}

	.doc-grid {
		margin-top: 1rem;
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 0.75rem;
	}

	.doc {
		border-radius: 0.65rem;
		border: 1px solid rgba(255, 255, 255, 0.12);
		background: rgba(0, 0, 0, 0.12);
		padding: 0.9rem 1rem;
	}

	.doc h4 {
		margin: 0;
	}

	@media (max-width: 900px) {
		.two-col {
			grid-template-columns: 1fr;
		}

		.week-grid {
			grid-template-columns: 1fr;
		}

		.doc-grid {
			grid-template-columns: 1fr;
		}
	}

	.forlob-nav {
		display: flex;
		flex-wrap: wrap;
		justify-content: space-between;
		align-items: center;
		gap: 1rem;
		margin-top: 2rem;
		padding-top: 2rem;
		border-top: 1px solid rgba(255, 255, 255, 0.1);
	}
</style>
