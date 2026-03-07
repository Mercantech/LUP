<script lang="ts">
	import '../app.css';
	import favicon from '$lib/assets/favicon.svg';

	let { children } = $props();
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
	<title>LUP – Datatekniker med speciale i programmering | Mercantec</title>
</svelte:head>

<header class="header">
	<nav class="container">
		<a href="/" class="logo"><span class="lup-l">L</span><span class="lup-u">U</span><span class="lup-p">P</span></a>
		<span class="subtitle">Datatekniker · Mercantec</span>
	</nav>
</header>

<main>
	{@render children()}
</main>

<footer class="footer">
	<div class="container">
		<p>LUP.DK – Lokal Undervisningsplan · Datatekniker med speciale i programmering · Mercantec</p>
	</div>
</footer>

<style>
	.header {
		background: var(--mercantec-blå-mørk);
		padding: 1rem 0;
		border-bottom: 1px solid rgba(255, 255, 255, 0.1);
	}

	.header nav {
		display: flex;
		align-items: baseline;
		gap: 1rem;
	}

	.logo {
		font-weight: 700;
		font-size: 1.25rem;
	}

	.logo .lup-l,
	.logo .lup-u,
	.logo .lup-p {
		color: var(--mercantec-blå-lys);
	}

	.subtitle {
		color: var(--grå-tekst);
		font-size: 0.9rem;
	}

	main {
		min-height: calc(100vh - 120px);
		padding: 2rem 0;
	}

	.footer {
		background: var(--mercantec-blå-mørk);
		padding: 1.5rem 0;
		border-top: 1px solid rgba(255, 255, 255, 0.1);
	}

	.footer p {
		margin: 0;
		color: var(--grå-tekst);
		font-size: 0.875rem;
	}
</style>
