<script lang="ts">
	import Roadmap from '$lib/components/Roadmap.svelte';
	import { FORLOB_PLACEHOLDER } from '$lib/data/forlob';
</script>

<div class="container">
	<div class="hero">
		<h1>
			<span class="lup-l">L</span><span class="lup-u">U</span><span class="lup-p">P</span> –
			<span class="lup-l">L</span>okal <span class="lup-u">U</span>ndervisnings<span class="lup-p">P</span>lan
		</h1>
		<p>Datatekniker med speciale i programmering · Mercantec Viborg</p>
	</div>

	<Roadmap forlob={FORLOB_PLACEHOLDER} />
</div>

<style>
	.hero {
		text-align: center;
		margin-bottom: 3rem;
	}

	.hero h1 {
		font-size: 2.5rem;
		font-weight: 700;
		margin: 0 0 0.5rem;
	}

	.hero h1 .lup-l,
	.hero h1 .lup-u,
	.hero h1 .lup-p {
		color: var(--mercantec-blå-lys);
	}

	.hero p {
		color: var(--grå-tekst);
		font-size: 1.1rem;
		margin: 0;
	}
</style>
