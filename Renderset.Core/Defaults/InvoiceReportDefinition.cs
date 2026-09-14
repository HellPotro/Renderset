using Renderset.Core.Definitions;

namespace Renderset.Core.Defaults;

public static class InvoiceReportDefinition
{
    public static ReportDefinition Create()
    {
        return new ReportDefinition
        {
            Id = "invoice",
            Name = "Factura cliente",

            Sections =
            [
                new ReportSectionDefinition
                {
                    Id = "customer",
                    Name = "Cliente",
                    Order = 10,

                    Fields =
                    [
                        new()
                        {
                            Id = "customer.code",
                            Label = "Cliente",
                            DataPath = "customer.code",
                            Order = 10
                        },
                        new()
                        {
                            Id = "customer.name",
                            Label = "Nombre",
                            DataPath = "customer.name",
                            Order = 20
                        },
                        new()
                        {
                            Id = "customer.vat",
                            Label = "CIF/NIF",
                            DataPath = "customer.vat",
                            Order = 30
                        },
                        new()
                        {
                            Id = "customer.address",
                            Label = "Dirección",
                            DataPath = "customer.address",
                            Order = 40
                        },
                        new()
                        {
                            Id = "customer.email",
                            Label = "Email",
                            DataPath = "customer.email",
                            Order = 50,
                            Type = ReportFieldType.Email
                        }
                    ]
                },

                new ReportSectionDefinition
                {
                    Id = "lines",
                    Name = "Líneas",
                    Order = 20,

                    Table = new ReportTableDefinition
                    {
                        Id = "invoice.lines",
                        Name = "Detalle factura",
                        DataPath = "lines",

                        Columns =
                        [
                            new()
                            {
                                Id = "lines.reference",
                                Label = "Referencia",
                                DataPath = "reference",
                                Order = 10
                            },
                            new()
                            {
                                Id = "lines.description",
                                Label = "Descripción",
                                DataPath = "description",
                                Order = 20
                            },
                            new()
                            {
                                Id = "lines.quantity",
                                Label = "Cantidad",
                                DataPath = "quantity",
                                Order = 30,
                                Type = ReportFieldType.Number
                            },
                            new()
                            {
                                Id = "lines.price",
                                Label = "Precio",
                                DataPath = "price",
                                Order = 40,
                                Type = ReportFieldType.Currency
                            },
                            new()
                            {
                                Id = "lines.discount",
                                Label = "Descuento",
                                DataPath = "discount",
                                Order = 50,
                                Type = ReportFieldType.Percentage
                            },
                            new()
                            {
                                Id = "lines.total",
                                Label = "Total",
                                DataPath = "total",
                                Order = 60,
                                Type = ReportFieldType.Currency
                            }
                        ]
                    }
                }
            ]
        };
    }
}