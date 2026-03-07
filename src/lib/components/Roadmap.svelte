<script lang="ts">
	import type { Forlob } from '$lib/types/forlob';

	interface Props {
		forlob: Forlob[];
	}

	let { forlob }: Props = $props();

	// Slange-layout: vertikal stak, noder skifter venstre/højre om en central linje (roadmap.sh Backend-stil)
</script>

<section class="roadmap">
	<h2 class="roadmap-title">Uddannelsesforløb</h2>
	<p class="roadmap-subtitle">Datatekniker med speciale i programmering – følg slangen fra GF2 til H6</p>

	<div class="roadmap-snake">
		<div class="spine" aria-hidden="true"></div>

		{#each forlob as item, i}
			{@const isLeft = i % 2 === 0}
			<div class="roadmap-row" class:row-left-node={!isLeft}>
				<!-- Venstre halvdel (50%) – node her når isLeft=false -->
				<div class="half half-left">
					{#if !isLeft}
						<a href="/forlob/{item.slug}" class="roadmap-node">
							<span class="node-step">{i}</span>
							<span class="node-label">{item.slug.toUpperCase()}</span>
							<span class="node-titel">{item.titel}</span>
							{#if item.beskrivelse}
								<span class="node-beskrivelse">{item.beskrivelse}</span>
							{/if}
							<span class="node-arrow" aria-hidden="true">→</span>
						</a>
					{/if}
				</div>

				<!-- Midten: snor fra spine til boks (rører begge) -->
				<div class="connector-column">
					{#if isLeft}
						<div class="connector-wrap connector-to-right" aria-hidden="true">
							<svg viewBox="0 0 100 40" preserveAspectRatio="none">
								<path d="M 0 20 C 20 30, 80 10, 100 20" fill="none" stroke="rgba(255,255,255,0.6)" stroke-width="2.5" stroke-linecap="round" />
								<circle cx="100" cy="20" r="3" fill="rgba(255,255,255,0.7)" />
							</svg>
						</div>
					{:else}
						<!-- Snor fra boks (venstre x=0) til spine (højre x=100) – H1, H3, H5 -->
						<div class="connector-wrap connector-to-left" aria-hidden="true">
							<svg viewBox="0 0 100 40" preserveAspectRatio="none">
								<path d="M 0 20 C 20 30, 80 10, 100 20" fill="none" stroke="rgba(255,255,255,0.6)" stroke-width="2.5" stroke-linecap="round" />
								<circle cx="0" cy="20" r="3" fill="rgba(255,255,255,0.7)" />
							</svg>
						</div>
					{/if}
				</div>

				<!-- Højre halvdel (50%) – node her når isLeft=true -->
				<div class="half half-right">
					{#if isLeft}
						<a href="/forlob/{item.slug}" class="roadmap-node">
							<span class="node-step">{i}</span>
							<span class="node-label">{item.slug.toUpperCase()}</span>
							<span class="node-titel">{item.titel}</span>
							{#if item.beskrivelse}
								<span class="node-beskrivelse">{item.beskrivelse}</span>
							{/if}
							<span class="node-arrow" aria-hidden="true">→</span>
						</a>
					{/if}
				</div>
			</div>

			{#if i < forlob.length - 1}
				<div class="spine-segment" aria-hidden="true">
					<svg viewBox="0 0 100 32" preserveAspectRatio="none" class="spine-svg">
						<!-- Lodret forbindelse ned ad spine – ingen udadgående kurve -->
						<path
							d="M 50 0 L 50 32"
							fill="none"
							stroke="rgba(255,255,255,0.5)"
							stroke-width="2"
							stroke-linecap="round"
						/>
					</svg>
				</div>
			{/if}
		{/each}
	</div>
</section>

<style>
	.roadmap {
		padding: 2rem 0 4rem;
	}

	.roadmap-title {
		font-size: 2rem;
		font-weight: 700;
		margin: 0 0 0.5rem;
		text-align: center;
	}

	.roadmap-subtitle {
		color: var(--grå-tekst);
		margin: 0 0 3rem;
		font-size: 1.1rem;
		text-align: center;
	}

	.roadmap-snake {
		position: relative;
		--connector-width: 100px;
		max-width: 720px;
		margin: 0 auto;
		padding: 0 1rem;
	}

	.spine {
		position: absolute;
		left: 50%;
		top: 0;
		bottom: 0;
		width: 4px;
		transform: translateX(-50%);
		background: linear-gradient(
			to bottom,
			transparent,
			rgba(255, 255, 255, 0.5) 5%,
			rgba(255, 255, 255, 0.5) 95%,
			transparent
		);
		border-radius: 2px;
	}

	.spine-segment {
		height: 32px;
		flex-shrink: 0;
		display: flex;
		justify-content: center;
		align-items: stretch;
	}

	.spine-segment .spine-svg {
		width: 100%;
		max-width: 180px;
		height: 100%;
	}

	.roadmap-row {
		display: flex;
		align-items: center;
		min-height: 100px;
		gap: 0;
	}

	/* Venstre halvdel = 50%, så spine (left:50%) falder præcis på connector-columns start */
	.half {
		display: flex;
		align-items: center;
		min-height: 80px;
	}

	.half-left {
		width: 50%;
		flex-shrink: 0;
		justify-content: flex-end;
		padding-right: 0;
		align-items: center;
	}

	.half-right {
		flex: 1;
		justify-content: flex-start;
		padding-left: 0;
		min-width: 0;
		align-items: center;
	}

	/* Snorene sidder i midten og rører spine (venstre kant) og boksen (højre/venstre kant) */
	.connector-column {
		width: var(--connector-width);
		flex-shrink: 0;
		height: 44px;
		display: flex;
		align-items: center;
		justify-content: stretch;
		min-height: 44px;
		overflow: visible;
	}

	.connector-wrap {
		width: 100%;
		height: 100%;
	}

	.connector-wrap svg {
		width: 100%;
		height: 100%;
	}

	/* Venstre-node række: halvdel er smallere så node ender præcis ved connector (som højre) */
	.row-left-node .half-left {
		width: calc(50% - var(--connector-width));
	}

	.connector-to-left {
		/* Samme kurve og styling som connector-to-right */
	}

	.roadmap-node {
		position: relative;
		display: flex;
		flex-direction: column;
		align-items: flex-start;
		padding: 1.25rem 1.5rem;
		min-width: 240px;
		max-width: 320px;
		background: linear-gradient(145deg, rgba(255, 255, 255, 0.14) 0%, rgba(255, 255, 255, 0.06) 100%);
		border: 1px solid rgba(255, 255, 255, 0.28);
		border-radius: 12px;
		box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
		transition: all 0.25s ease;
	}

	.roadmap-node:hover {
		background: linear-gradient(145deg, rgba(255, 255, 255, 0.22) 0%, rgba(255, 255, 255, 0.1) 100%);
		border-color: rgba(255, 255, 255, 0.5);
		transform: translateY(-4px) scale(1.02);
		box-shadow: 0 12px 32px rgba(0, 0, 0, 0.3), 0 0 0 1px rgba(255, 255, 255, 0.1);
	}

	.roadmap-node:hover .node-arrow {
		transform: translateX(4px);
		opacity: 1;
	}

	.node-step {
		position: absolute;
		top: -10px;
		left: 1rem;
		display: flex;
		align-items: center;
		justify-content: center;
		width: 26px;
		height: 26px;
		background: var(--mercantec-blå-lys);
		color: var(--hvid);
		font-size: 0.8rem;
		font-weight: 700;
		border-radius: 50%;
		box-shadow: 0 2px 10px rgba(59, 130, 246, 0.45);
	}

	.node-label {
		font-weight: 700;
		font-size: 0.72rem;
		letter-spacing: 0.08em;
		color: var(--mercantec-blå-lys);
		margin-bottom: 0.35rem;
		margin-top: 0.5rem;
	}

	.node-titel {
		font-weight: 600;
		font-size: 1.05rem;
		line-height: 1.3;
	}

	.node-beskrivelse {
		font-size: 0.82rem;
		color: var(--grå-tekst);
		margin-top: 0.5rem;
		line-height: 1.35;
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}

	.node-arrow {
		position: absolute;
		bottom: 1rem;
		right: 1rem;
		font-size: 1.25rem;
		opacity: 0.6;
		transition: transform 0.2s, opacity 0.2s;
	}

	@media (max-width: 640px) {
		.roadmap-snake {
			max-width: 100%;
			padding: 0 0.5rem;
		}

		.spine {
			left: 1rem;
			transform: translateX(-50%);
		}

		.roadmap-row {
			flex-wrap: wrap;
			padding-left: 2rem;
		}

		.half-left {
			width: 100%;
			justify-content: flex-start;
			order: 2;
		}

		.half-right {
			width: 100%;
			order: 2;
		}

		.connector-column {
			width: 24px;
			height: 24px;
			order: 1;
			align-self: flex-start;
			margin-left: -0.5rem;
			margin-top: 2rem;
		}

		.connector-column .connector-wrap svg {
			transform: rotate(90deg);
		}

		.connector-column .connector-to-left svg {
			transform: rotate(90deg) scaleX(-1);
		}

		.spine-segment {
			justify-content: flex-start;
			padding-left: 1rem;
		}

		.spine-segment .spine-svg {
			max-width: 80px;
			margin-left: -0.5rem;
		}

		.roadmap-node {
			min-width: 0;
			max-width: 100%;
			width: 100%;
		}
	}
</style>
