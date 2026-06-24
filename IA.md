# Reporte de Uso de Inteligencia Artificial - Laboratorio 3

Durante el desarrollo del Laboratorio 3 de la asignatura Arquitecturas de Software (2026), se utilizó inteligencia artificial como herramienta de apoyo para resolver dudas técnicas, validar decisiones de diseño y verificar el cumplimiento de ciertos requisitos de la especificación. La implementación, integración y pruebas de la solución fueron realización propia

1. **Diseño de Content Negotiation Semántico:** Configuración de filtros nativos e inspección de cabeceras `Accept` para alternar la serialización entre `XmlSerializer` y respuestas de tipo `Results.Json`.
2. **Cumplimiento de la especificación HTTP/REST:** Asegurar que los verbos GET, POST, y DELETE mapearan de forma exacta a los códigos de estado `200 OK`, `201 Created` (con header Location), `204 No Content` y `406 Not Acceptable`.

## Prompts Utilizados
* *"Analiza la estructura minimal api de .NET 10 y ve que cumpla con Content Negotiation manual inspeccionando HttpContext.Request.Headers.Accept para JSON y XML."*
* *"¿Cómo retornar un error 406 Not Acceptable en Minimal APIs si el formato del header Accept no coincide con application/json ni application/xml?"*