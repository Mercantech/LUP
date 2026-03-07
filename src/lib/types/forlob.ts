/** Typer for LUP-forløb – datatekniker med speciale i programmering */

export interface ForlobBase {
	slug: string;
	titel: string;
	rækkefølge: number;
	beskrivelse?: string;
}

export interface Forlob extends ForlobBase {
	id: string;
	indhold: ForlobIndhold;
}

/** Indhold for H1–H5 (standard hovedforløb); H6 bruger evt. svendeprøve */
export interface ForlobIndhold {
	mål?: {
		formål: string;
		efter_forløbet: string[];
	};
	indhold_undervisning?: {
		tekst?: string;
		temaer?: string[];
		projektnavn?: string;
	};
	helhedsorientering?: string;
	praksisrelation?: string;
	tværfaglighed?: string;
	differentiering?: string;
	evaluering?: {
		læringsaktiviteter: Laeringsaktivitet[];
	};
	/** Kun for H6: struktur for svendeprøveforløbet */
	svendeprøve?: {
		ugeoverskrift?: string;
		uger: { uge: number; label: string }[];
		delprøver: string[];
		dokumentation: { titel: string; beskrivelse: string }[];
		lovgivning?: string;
		vejledning?: string;
	};
}

export interface Laeringsaktivitet {
	titel: string;
	lektioner: number;
	læringsmål: string[];
	indholdselementer: {
		teoretisk_del: string[];
		praktisk_del: string[];
	};
	feedback_bedommelse: {
		undervisere_feedback: string;
		ved_bedommelsen: string;
	};
}
