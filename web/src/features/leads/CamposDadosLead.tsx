import type { FieldError, FieldErrors, Path, UseFormRegister } from 'react-hook-form'
import type { AvisoLead } from '@/api/cliente'
import { CampoSelecao } from '@/components/CampoSelecao'
import { CampoTexto } from '@/components/CampoTexto'
import { UFS } from '@/lib/ufs'

type Dados = {
  nome: string
  email: string
  telefone: string
  cpfCnpj: string
  uf: string
  empresa: string
}

type Props<T extends Dados> = {
  register: UseFormRegister<T>
  errors: FieldErrors<T>
  /** Avisos da ficha sobre os valores salvos, mostrados embaixo de cada campo. */
  avisos?: AvisoLead[]
}

/** Nome, contato, documento, UF e empresa: os mesmos campos no novo lead e na ficha. */
export function CamposDadosLead<T extends Dados>({ register, errors, avisos = [] }: Props<T>) {
  const campo = (nome: keyof Dados) => register(nome as Path<T>)
  const erro = (nome: keyof Dados) => errors[nome] as FieldError | undefined
  const aviso = (nome: keyof Dados) => avisos.find((a) => a.campo === nome)?.mensagem

  return (
    <div className="grid gap-5 md:grid-cols-2">
      <div className="md:col-span-2">
        <CampoTexto
          id="nome"
          rotulo="Nome"
          autoComplete="off"
          registro={campo('nome')}
          erro={erro('nome')}
        />
      </div>
      <CampoTexto
        id="telefone"
        rotulo="Telefone"
        type="tel"
        inputMode="tel"
        autoComplete="off"
        placeholder="(51) 99999-8888"
        registro={campo('telefone')}
        erro={erro('telefone')}
        aviso={aviso('telefone')}
      />
      <CampoTexto
        id="email"
        rotulo="E-mail"
        type="email"
        autoComplete="off"
        registro={campo('email')}
        erro={erro('email')}
        aviso={aviso('email')}
      />
      <CampoTexto
        id="cpfCnpj"
        rotulo="CPF ou CNPJ"
        autoComplete="off"
        registro={campo('cpfCnpj')}
        erro={erro('cpfCnpj')}
        aviso={aviso('cpfCnpj')}
      />
      <CampoSelecao id="uf" rotulo="UF" registro={campo('uf')} erro={erro('uf')}>
        <option value="">Não informada</option>
        {UFS.map((uf) => (
          <option key={uf.sigla} value={uf.sigla}>
            {uf.sigla} · {uf.nome}
          </option>
        ))}
      </CampoSelecao>
      <div className="md:col-span-2">
        <CampoTexto
          id="empresa"
          rotulo="Empresa"
          autoComplete="off"
          registro={campo('empresa')}
          erro={erro('empresa')}
        />
      </div>
    </div>
  )
}
