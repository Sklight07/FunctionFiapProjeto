using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace FunctionFiapProjeto
{
    public static class Function1
    {
        [Function(nameof(Function1))]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(Function1));
            var outputs = new List<string>();

            var parametros = context.GetInput<PedidosAprovacaoDTO>();
            var idPedido = parametros?.IdPedido;
            var valor = parametros?.Valor;

            var dados = parametros;

            // ProcessarEtapaAprovacao --> PedidoCriado
            var resultadoPedidoCriado = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaAprovacao), dados);
            outputs.Add("PedidoCriado " + resultadoPedidoCriado.ToString());

            logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoPedidoCriado}");

            // ProcessarEtapaAprovacao --> PedidoEmFilaDeAnalise
            var resultadoPedidoEmFilaDeAnalise = false;
            if (resultadoPedidoCriado)
            {
                dados.Etapa = EnuPedidosEtapaAprovacao.PedidoEmFilaDeAnalise;


                resultadoPedidoEmFilaDeAnalise = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaAprovacao), dados);
                outputs.Add("PedidoEmFilaDeAnalise " + resultadoPedidoEmFilaDeAnalise.ToString());

                logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoPedidoEmFilaDeAnalise}");
            }

            // ProcessarEtapaAprovacao --> PedidoAtribuidoParaAnalie
            var resultadoPedidoAtribuidoParaAnalise = false;
            if (resultadoPedidoEmFilaDeAnalise)
            {
                dados.Etapa = EnuPedidosEtapaAprovacao.PedidoAtribuidoParaAnalie;

                resultadoPedidoAtribuidoParaAnalise = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaAprovacao), dados);
                outputs.Add("PedidoAtribuidoParaAnalie " + resultadoPedidoAtribuidoParaAnalise.ToString());

                logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoPedidoAtribuidoParaAnalise}");
            }

            // ProcessarEtapaAprovacao --> PedidoEmAnalise
            var resultadoPedidoEmAnalise = false;
            if (resultadoPedidoAtribuidoParaAnalise)
            {
                dados.Etapa = EnuPedidosEtapaAprovacao.PedidoEmAnalise;

                resultadoPedidoEmAnalise = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaAprovacao), dados);
                outputs.Add("PedidoEmAnalise " + resultadoPedidoEmAnalise.ToString());

                logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoPedidoEmAnalise}");
            }

            // ProcessarEtapaAprovacao --> PedidoEmValidacaoDePreco
            var resultadoPedidoEmValidacaoDePreco = false;
            if (resultadoPedidoEmAnalise)
            {
                dados.Etapa = EnuPedidosEtapaAprovacao.PedidoEmValidacaoDePreco;

                resultadoPedidoEmValidacaoDePreco = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaAprovacao), dados);
                outputs.Add("PedidoEmValidacaoDePreco " + resultadoPedidoEmValidacaoDePreco.ToString());

                logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoPedidoEmValidacaoDePreco}");
            }

            // ProcessarEtapaAprovacao --> AprovacaoFinalizada
            var resultadoAprovacaoFinalizada = false;
            if (resultadoPedidoEmValidacaoDePreco)
            {
                dados.Etapa = EnuPedidosEtapaAprovacao.AprovacaoFinalizada;

                resultadoAprovacaoFinalizada = await context.CallActivityAsync<bool>(nameof(ProcessarEtapaAprovacao), dados);
                outputs.Add("AprovacaoFinalizada " + resultadoAprovacaoFinalizada.ToString());

                logger.LogInformation($"Aprovação de Pedido... id {idPedido} valor {valor} etapa {dados.Etapa} resultado {resultadoAprovacaoFinalizada}");
            }

            return outputs;
        }

        [Function(nameof(ProcessarEtapaAprovacao))]
        public static bool ProcessarEtapaAprovacao([ActivityTrigger] PedidosAprovacaoDTO dados, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("ProcessarEtapaAprovacao");

            //--: var idPedido = Convert.ToInt32(dados.Split('|')[0]);
            //--: var valor = Convert.ToDecimal(dados.Split('|')[1]);
            //--: var etapa = dados.Split('|')[2];

            var idPedido = dados.IdPedido;
            var valor = dados.Valor;
            var etapa = dados.Etapa.ToString();

            logger.LogInformation($"Processando Aprovação do Pedido {idPedido}, valor {valor}, etapa {etapa}.");
            //--> if (etapa != "PedidoEmValidacaoDePreco")

            if (dados.Etapa != EnuPedidosEtapaAprovacao.PedidoEmValidacaoDePreco)
                return true;
            else
                return valor <= 500000.00M;
        }

        [Function("Function1_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Function1_HttpStart");

            var parametros = new PedidosAprovacaoDTO();
            parametros.IdPedido = Convert.ToInt32(req.Query["idPedido"]);
            parametros.Valor = Convert.ToDecimal(req.Query["valor"]);
            parametros.Etapa = EnuPedidosEtapaAprovacao.PedidoCriado;

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Function1), parametros);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            HttpResponseData resposta = client.CreateCheckStatusResponse(req, instanceId);
            return resposta;
        }
    }

    public class PedidosAprovacaoDTO : ISerializable
    {
        public int IdPedido { get; set; }
        public Decimal Valor { get; set; }

        public EnuPedidosEtapaAprovacao Etapa { get; set; }

        public bool ResultadoProcessamento { get; set; }

        public PedidosAprovacaoDTO(int idPedido, decimal valorPedido, EnuPedidosEtapaAprovacao etapaAprovacao)
        {
            IdPedido = idPedido;
            Valor = valorPedido;
            Etapa = etapaAprovacao;
        }

        public PedidosAprovacaoDTO()
        {
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("IdPedido", IdPedido);
            info.AddValue("Valor", Valor);
            info.AddValue("Etapa", Etapa.ToString());
        }
    }

    public enum EnuPedidosEtapaAprovacao
    {
        [Description("PedidoCriado")]
        PedidoCriado = 1,
        [Description("PedidoEmFilaDeAnalise")]
        PedidoEmFilaDeAnalise = 2,
        [Description("PedidoAtribuidoParaAnalise")]
        PedidoAtribuidoParaAnalie = 3,
        [Description("PedidoEmAnalise")]
        PedidoEmAnalise = 4,
        [Description("PedidoEmValidacaoDePreco")]
        PedidoEmValidacaoDePreco = 5,
        [Description("AprovacaoFinalizada")]
        AprovacaoFinalizada = 6
    }
}
