using Xunit;

// As fixtures de integracao usam Testcontainers + Environment.SetEnvironmentVariable
// (process-wide). Rodar colecoes em paralelo causa race condition na connection
// string e migrations concorrentes no mesmo container — viola constraints internos
// do Postgres (pg_type_typname_nsp_index). Forcamos execucao serial.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
