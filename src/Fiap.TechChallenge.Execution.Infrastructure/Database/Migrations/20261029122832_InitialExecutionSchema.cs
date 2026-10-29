using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiap.TechChallenge.Execution.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialExecutionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ordens_producao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_servico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filial_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    data_abertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_liberacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_conclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cliente_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    veiculo_ano = table.Column<int>(type: "integer", nullable: false),
                    veiculo_modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    veiculo_placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_producao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "diagnosticos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_producao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_mecanico = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_mecanico = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    data_hora_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_hora_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    laudo_tecnico = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnosticos", x => x.id);
                    table.ForeignKey(
                        name: "fk_diagnostico_ordem_producao",
                        column: x => x.ordem_producao_id,
                        principalTable: "ordens_producao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historico_ordens_producao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_producao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    data_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_ordens_producao", x => x.id);
                    table.ForeignKey(
                        name: "fk_historico_ordem_producao",
                        column: x => x.ordem_producao_id,
                        principalTable: "ordens_producao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tarefas_producao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem_producao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_servico_origem = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tempo_estimado_minutos = table.Column<int>(type: "integer", nullable: false),
                    id_mecanico_responsavel = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_mecanico_responsavel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDENTE"),
                    data_hora_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    data_hora_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observacoes_tecnicas = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarefas_producao", x => x.id);
                    table.CheckConstraint("ck_tarefas_tempo_estimado_positivo", "tempo_estimado_minutos > 0");
                    table.ForeignKey(
                        name: "fk_tarefas_ordem_producao",
                        column: x => x.ordem_producao_id,
                        principalTable: "ordens_producao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diagnostico_pecas_sugeridas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_peca_catalogo = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostico_pecas_sugeridas", x => x.id);
                    table.CheckConstraint("ck_diag_pecas_quantidade_positiva", "quantidade > 0");
                    table.ForeignKey(
                        name: "fk_diag_pecas_diagnostico",
                        column: x => x.diagnostico_id,
                        principalTable: "diagnosticos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diagnostico_servicos_sugeridos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnostico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_servico_catalogo = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnostico_servicos_sugeridos", x => x.id);
                    table.ForeignKey(
                        name: "fk_diag_servicos_diagnostico",
                        column: x => x.diagnostico_id,
                        principalTable: "diagnosticos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_diagnostico_pecas_sugeridas_diagnostico_id",
                table: "diagnostico_pecas_sugeridas",
                column: "diagnostico_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagnostico_servicos_sugeridos_diagnostico_id",
                table: "diagnostico_servicos_sugeridos",
                column: "diagnostico_id");

            migrationBuilder.CreateIndex(
                name: "uk_diagnostico_ordem_producao",
                table: "diagnosticos",
                column: "ordem_producao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_historico_ordem",
                table: "historico_ordens_producao",
                columns: new[] { "ordem_producao_id", "data_hora" });

            migrationBuilder.CreateIndex(
                name: "idx_ordens_producao_fila_diag",
                table: "ordens_producao",
                column: "data_abertura",
                filter: "status = 'AGUARDANDO_DIAGNOSTICO'");

            migrationBuilder.CreateIndex(
                name: "idx_ordens_producao_fila_prod",
                table: "ordens_producao",
                column: "data_liberacao",
                filter: "status = 'NA_FILA'");

            migrationBuilder.CreateIndex(
                name: "idx_ordens_producao_status",
                table: "ordens_producao",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uk_ordens_producao_os",
                table: "ordens_producao",
                column: "ordem_servico_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tarefas_mecanico_fim",
                table: "tarefas_producao",
                columns: new[] { "id_mecanico_responsavel", "data_hora_fim" },
                filter: "status = 'CONCLUIDA'");

            migrationBuilder.CreateIndex(
                name: "IX_tarefas_producao_ordem_producao_id",
                table: "tarefas_producao",
                column: "ordem_producao_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diagnostico_pecas_sugeridas");

            migrationBuilder.DropTable(
                name: "diagnostico_servicos_sugeridos");

            migrationBuilder.DropTable(
                name: "historico_ordens_producao");

            migrationBuilder.DropTable(
                name: "tarefas_producao");

            migrationBuilder.DropTable(
                name: "diagnosticos");

            migrationBuilder.DropTable(
                name: "ordens_producao");
        }
    }
}
